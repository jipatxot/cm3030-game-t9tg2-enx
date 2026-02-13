using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/// Precise blackout countdown UI (for shared LightPowerDecay component).
/// - Shows mm:ss at top of screen.
/// - Countdown becomes 00:00 when ALL counted lights are off.
/// - When A repairs (PowerRepairInteraction.OnRepaired), countdown recomputes immediately.
/// - "Precise" means it integrates PowerDecayManager.decayMultiplierOverTime over time (LUT + inversion),
///   rather than estimating with only the current multiplier.
public class BlackoutCountdownPreciseUI : MonoBehaviour
{
    [Header("UI (optional)")]
    [Tooltip("If null, script will auto-create a Canvas + Text at runtime.")]
    public Text countdownText;

    public string prefix = "Blackout in ";

    [Header("Precision")]
    [Range(256, 16384)] public int integralSamples = 4096;

    [Header("Recompute")]
    public bool recomputeOnLampCountChange = true;
    [Min(0.05f)] public float lampCountCheckInterval = 0.5f;

    [Tooltip("Treat power <= this as 'off'.")]
    [Min(0f)] public float offThresholdPower = 0.001f;

    [Header("Edge Cases")]
    public bool showInfinityWhenNeverBlackout = true;

    // Target blackout time expressed in PowerDecayManager.ElapsedSeconds timeline
    private float _targetBlackoutElapsed = 0f;
    private bool _targetValid = false;
    private bool _neverBlackout = false;

    // LUT for integral of multiplier curve over normalized time [0..1]
    private float[] _cumIntegral;
    private float _integralTotal; // I(1)
    private float _multEnd;       // curve(1)

    private int _lastCount = -1;
    private float _countTimer = 0f;

    private void OnEnable()
    {
        PowerRepairInteraction.OnRepaired += HandleRepaired;
    }

    private void OnDisable()
    {
        PowerRepairInteraction.OnRepaired -= HandleRepaired;
    }

    private void Awake()
    {
        EnsureText();
    }

    private void Start()
    {
        BuildIntegralLUT();
        RecomputeTarget();
    }

    private void Update()
    {
        var mgr = PowerDecayManager.Instance;

        if (mgr == null)
        {
            if (EnsureText())
                countdownText.text = prefix + "00:00";
            return;
        }

        if (!EnsureText()) return;

        if (recomputeOnLampCountChange)
        {
            _countTimer += Time.deltaTime;
            if (_countTimer >= lampCountCheckInterval)
            {
                _countTimer = 0f;
                int count = GetDevices().Count;
                if (count != _lastCount)
                {
                    _lastCount = count;
                    RecomputeTarget();
                }
            }
        }

        if (_neverBlackout && showInfinityWhenNeverBlackout)
        {
            countdownText.text = prefix + "∞";
            return;
        }

        float remaining = 0f;
        if (_targetValid)
            remaining = Mathf.Max(0f, _targetBlackoutElapsed - mgr.ElapsedSeconds);

        countdownText.text = prefix + FormatTimeMMSS(remaining);
    }

    private void HandleRepaired()
    {
        RecomputeTarget();
    }

    public void RecomputeTarget()
    {
        var mgr = PowerDecayManager.Instance;
        if (mgr == null)
        {
            _targetValid = false;
            _neverBlackout = false;
            _targetBlackoutElapsed = 0f;
            return;
        }

        BuildIntegralLUT();

        var devices = GetDevices();
        _lastCount = devices.Count;

        if (devices.Count == 0)
        {
            _targetValid = true;
            _neverBlackout = false;
            _targetBlackoutElapsed = mgr.ElapsedSeconds;
            return;
        }

        bool allOffNow = true;

        // If any device never drains but has power, blackout never happens
        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            if (d == null) continue;

            if (d.CurrentPower > offThresholdPower) allOffNow = false;

            if (d.baseDecayPerSecond <= 0f && d.CurrentPower > offThresholdPower)
            {
                _neverBlackout = true;
                _targetValid = false;
                return;
            }
        }

        if (allOffNow)
        {
            _neverBlackout = false;
            _targetValid = true;
            _targetBlackoutElapsed = mgr.ElapsedSeconds;
            return;
        }

        _neverBlackout = false;

        float nowElapsed = mgr.ElapsedSeconds;
        float maxT = 0f;

        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            if (d == null) continue;

            float p = d.CurrentPower;
            if (p <= offThresholdPower) continue;

            float baseDecay = d.baseDecayPerSecond;
            if (baseDecay <= 0f) continue;

            float requiredMultSeconds = p / baseDecay;
            float tToZero = SolveTimeForRequiredIntegral(nowElapsed, requiredMultSeconds);

            if (tToZero > maxT) maxT = tToZero;
        }

        _targetValid = true;
        _targetBlackoutElapsed = nowElapsed + maxT;
    }

    private float SolveTimeForRequiredIntegral(float nowElapsedSeconds, float required)
    {
        if (required <= 0f) return 0f;

        var mgr = PowerDecayManager.Instance;
        float sessionLen = Mathf.Max(0.0001f, mgr.sessionLengthSeconds);

        float n0 = Mathf.Clamp01(nowElapsedSeconds / sessionLen);

        if (nowElapsedSeconds >= sessionLen)
        {
            if (_multEnd <= 0f) return float.PositiveInfinity;
            return required / _multEnd;
        }

        float I0 = IntegralAt(n0);
        float I1 = _integralTotal;
        float remainingInCurve = sessionLen * (I1 - I0);

        if (required <= remainingInCurve)
        {
            float targetI = I0 + (required / sessionLen);

            float lo = n0;
            float hi = 1f;
            for (int it = 0; it < 40; it++)
            {
                float mid = (lo + hi) * 0.5f;
                float Imid = IntegralAt(mid);
                if (Imid < targetI) lo = mid;
                else hi = mid;
            }

            float n1Found = (lo + hi) * 0.5f;
            return (n1Found - n0) * sessionLen;
        }
        else
        {
            float leftover = required - remainingInCurve;
            if (_multEnd <= 0f) return float.PositiveInfinity;

            float tToEnd = (1f - n0) * sessionLen;
            float tTail = leftover / _multEnd;
            return tToEnd + tTail;
        }
    }

    private void BuildIntegralLUT()
    {
        var mgr = PowerDecayManager.Instance;
        if (mgr == null) return;

        int N = Mathf.Max(2, integralSamples);
        if (_cumIntegral == null || _cumIntegral.Length != N)
            _cumIntegral = new float[N];

        var curve = mgr.decayMultiplierOverTime;
        float dx = 1f / (N - 1);

        _cumIntegral[0] = 0f;
        float prevY = Mathf.Max(0f, curve.Evaluate(0f));
        float acc = 0f;

        for (int i = 1; i < N; i++)
        {
            float x = i * dx;
            float y = Mathf.Max(0f, curve.Evaluate(x));
            acc += (prevY + y) * 0.5f * dx; // trapezoid
            _cumIntegral[i] = acc;
            prevY = y;
        }

        _integralTotal = _cumIntegral[N - 1];
        _multEnd = Mathf.Max(0f, curve.Evaluate(1f));
    }

    private float IntegralAt(float n01)
    {
        if (_cumIntegral == null || _cumIntegral.Length < 2) return 0f;

        float n = Mathf.Clamp01(n01);
        int N = _cumIntegral.Length;

        float f = n * (N - 1);
        int i0 = Mathf.FloorToInt(f);
        int i1 = Mathf.Min(i0 + 1, N - 1);
        float t = f - i0;

        return Mathf.Lerp(_cumIntegral[i0], _cumIntegral[i1], t);
    }

    private static List<LightPowerDecay> GetDevices()
    {
        // Prefer manager registry (spawn-friendly)
        if (PowerDecayManager.Instance != null && PowerDecayManager.Instance.Lamps != null)
        {
            var src = PowerDecayManager.Instance.Lamps;
            var list = new List<LightPowerDecay>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                var d = src[i];
                if (d != null && d.countsTowardBlackout)
                    list.Add(d);
            }
            return list;
        }

#if UNITY_2023_1_OR_NEWER
        var found = Object.FindObjectsByType<LightPowerDecay>(FindObjectsSortMode.None);
#else
        var found = Object.FindObjectsOfType<LightPowerDecay>();
#endif
        var filtered = new List<LightPowerDecay>(found.Length);
        for (int i = 0; i < found.Length; i++)
        {
            var d = found[i];
            if (d != null && d.countsTowardBlackout)
                filtered.Add(d);
        }
        return filtered;
    }

    private static string FormatTimeMMSS(float seconds)
    {
        if (seconds <= 0f) return "00:00";

        int total = Mathf.CeilToInt(seconds);
        int mm = total / 60;
        int ss = total % 60;

        return TwoDigits(mm) + ":" + TwoDigits(ss);
    }

    private static string TwoDigits(int v)
    {
        if (v < 0) v = 0;
        if (v < 10) return "0" + v;
        return v.ToString();
    }

    private bool EnsureText()
    {
        if (countdownText != null) return true;

        CreateDefaultUI();
        return countdownText != null;
    }

    private void CreateDefaultUI()
    {
        var canvasGO = new GameObject("BlackoutCountdownCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("CountdownText");
        textGO.transform.SetParent(canvasGO.transform, false);

        var t = textGO.AddComponent<Text>();
        t.font = LoadBuiltinFontSafe();
        t.alignment = TextAnchor.UpperCenter;
        t.raycastTarget = false;
        t.fontSize = 28;
        t.text = prefix + "00:00";

        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(900f, 60f);

        countdownText = t;
    }

    private static Font LoadBuiltinFontSafe()
    {
        // Unity 6+ prefers LegacyRuntime.ttf; older versions may still have Arial.ttf.
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch { }

        try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        catch { }

        // Fallback: null is allowed (Text will still render in some setups),
        // but it's better if you assign a font in Inspector.
        return null;
    }
}
