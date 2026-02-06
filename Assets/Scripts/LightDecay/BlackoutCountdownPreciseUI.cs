using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/// Precise blackout countdown UI:
/// - Displays mm:ss at top of screen.
/// - Countdown is EXACT w.r.t. decayMultiplierOverTime curve integration (via precomputed integral + inversion).
/// - When ALL StreetLampPower are off => shows 00:00.
/// - When A repairs (or any external trigger calls RequestRecompute) => target time recomputed, countdown restarts.
public class BlackoutCountdownPreciseUI : MonoBehaviour
{
    [Header("UI (optional)")]
    [Tooltip("If null, script will auto-create a Canvas + Text at runtime.")]
    public Text countdownText;

    public string prefix = "Blackout in ";

    [Header("Precision / Recompute")]
    [Tooltip("Integral LUT samples. Higher = more precise but slightly more memory.")]
    [Range(256, 16384)] public int integralSamples = 4096;

    [Tooltip("Recompute automatically if lamp count changes.")]
    public bool recomputeOnLampCountChange = true;

    [Tooltip("How often (seconds) to check lamp count changes.")]
    [Min(0.05f)] public float lampCountCheckInterval = 0.5f;

    [Tooltip("Treat power <= this as 'off'.")]
    [Min(0f)] public float offThresholdPower = 0.001f;

    [Header("Edge Cases")]
    [Tooltip("If any lamp never drains (baseDecayPerSecond<=0) and has power, blackout never happens.")]
    public bool showInfinityWhenNeverBlackout = true;

    // Target blackout time expressed in PowerDecayManager.ElapsedSeconds timeline
    private float _targetBlackoutElapsed = 0f;
    private bool _targetValid = false;
    private bool _neverBlackout = false;

    // LUT for integral of multiplier curve over normalized time [0..1]
    private float[] _cumIntegral; // length = integralSamples
    private float _integralTotal; // I(1)
    private float _multEnd;       // curve(1)

    private int _lastLampCount = -1;
    private float _lampCountTimer = 0f;

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
        if (countdownText == null)
            CreateDefaultUI();
    }

    private void Start()
    {
        BuildIntegralLUT();
        RecomputeTarget();
    }

    private void Update()
    {
        var mgr = PowerDecayManager.Instance;

        // No Manager: Displays 00:00 (if it can be displayed).
        if (mgr == null)
        {
            if (EnsureText())
                countdownText.text = prefix + "00:00";
            return;
        }

        // Ensure the UI exists; otherwise, return immediately (to avoid NRE).
        if (!EnsureText())
            return;

        // Optional: Changes in the number of detection lights (during urban reconstruction)
        if (recomputeOnLampCountChange)
        {
            _lampCountTimer += Time.deltaTime;
            if (_lampCountTimer >= lampCountCheckInterval)
            {
                _lampCountTimer = 0f;
                int count = GetLamps().Count;
                if (count != _lastLampCount)
                {
                    _lastLampCount = count;
                    RecomputeTarget();
                }
            }
        }

        // Never completely off: Display ∞
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


    
    private bool EnsureText()
    {
        if (countdownText != null) return true;

        CreateDefaultUI();

        // Check again after CreateDefaultUI
        return countdownText != null;
    }


    /// Call this whenever you change lamp power externally (e.g., repair).
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

        BuildIntegralLUT(); // safe to call; will rebuild if curve/params changed

        var lamps = GetLamps();
        _lastLampCount = lamps.Count;

        if (lamps.Count == 0)
        {
            _targetValid = true;
            _neverBlackout = false;
            _targetBlackoutElapsed = mgr.ElapsedSeconds;
            return;
        }

        // If all already off => 0
        bool allOffNow = true;

        // If any lamp never drains but has power => blackout never happens
        for (int i = 0; i < lamps.Count; i++)
        {
            var l = lamps[i];
            if (l == null) continue;

            if (l.CurrentPower > offThresholdPower) allOffNow = false;

            if (l.baseDecayPerSecond <= 0f && l.CurrentPower > offThresholdPower)
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

        // Compute exact time-to-zero for each lamp, take the MAX (last one to die)
        float nowElapsed = mgr.ElapsedSeconds;
        float maxT = 0f;

        for (int i = 0; i < lamps.Count; i++)
        {
            var l = lamps[i];
            if (l == null) continue;

            float p = l.CurrentPower;
            if (p <= offThresholdPower) continue; // already off

            float baseDecay = l.baseDecayPerSecond;
            if (baseDecay <= 0f) continue; // handled above as never-blackout

            // Need integral(mult) over time = p / baseDecay
            float requiredMultSeconds = p / baseDecay;

            float tToZero = SolveTimeForRequiredIntegral(nowElapsed, requiredMultSeconds);
            if (tToZero > maxT) maxT = tToZero;
        }

        _targetValid = true;
        _targetBlackoutElapsed = nowElapsed + maxT;
    }

    private void HandleRepaired()
    {
        // After A repair, power jumps up -> recompute target
        RecomputeTarget();
    }


    /// Solve smallest T>=0 such that ∫_{now}^{now+T} mult(s) ds = required.
    /// mult(s) is defined by the curve on [0..sessionLength], then clamped to curve(1) afterwards.
    private float SolveTimeForRequiredIntegral(float nowElapsedSeconds, float required)
    {
        if (required <= 0f) return 0f;

        var mgr = PowerDecayManager.Instance;
        float sessionLen = Mathf.Max(0.0001f, mgr.sessionLengthSeconds);

        // normalized current time
        float n0 = Mathf.Clamp01(nowElapsedSeconds / sessionLen);

        // If we're already past the session end, mult is constant at end value
        if (nowElapsedSeconds >= sessionLen)
        {
            if (_multEnd <= 0f) return float.PositiveInfinity;
            return required / _multEnd;
        }

        // integral capacity remaining within the curve region
        float I0 = IntegralAt(n0);
        float I1 = _integralTotal;
        float remainingInCurve = sessionLen * (I1 - I0);

        if (required <= remainingInCurve)
        {
            // Find n1 in [n0, 1] such that sessionLen*(I(n1)-I(n0)) = required
            float targetI = I0 + (required / sessionLen);

            float lo = n0;
            float hi = 1f;

            // 30-40 iterations are enough for float precision
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
            // Use remaining curve integral then linear tail
            float leftover = required - remainingInCurve;
            if (_multEnd <= 0f) return float.PositiveInfinity;

            float tToEnd = (1f - n0) * sessionLen;
            float tTail = leftover / _multEnd;
            return tToEnd + tTail;
        }
    }


    /// Build cumulative integral LUT of decayMultiplierOverTime on normalized [0..1].
    private void BuildIntegralLUT()
    {
        var mgr = PowerDecayManager.Instance;
        if (mgr == null) return;

        int N = Mathf.Max(2, integralSamples);

        // Build if not exists or size changed
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
            // trapezoid
            acc += (prevY + y) * 0.5f * dx;
            _cumIntegral[i] = acc;
            prevY = y;
        }

        _integralTotal = _cumIntegral[N - 1];
        _multEnd = Mathf.Max(0f, curve.Evaluate(1f));
    }


    /// Integral I(n) = ∫_0^n curve(u) du, using LUT linear interpolation.
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

    private static List<StreetLampPower> GetLamps()
    {
        if (PowerDecayManager.Instance != null && PowerDecayManager.Instance.Lamps != null)
        {
            var list = new List<StreetLampPower>(PowerDecayManager.Instance.Lamps.Count);
            for (int i = 0; i < PowerDecayManager.Instance.Lamps.Count; i++)
            {
                var l = PowerDecayManager.Instance.Lamps[i];
                if (l != null) list.Add(l);
            }
            return list;
        }

#if UNITY_2023_1_OR_NEWER
        return new List<StreetLampPower>(
            Object.FindObjectsByType<StreetLampPower>(FindObjectsSortMode.None)
        );
#else
        return new List<StreetLampPower>(Object.FindObjectsOfType<StreetLampPower>());
#endif
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
        Font f = null;
        try
        {
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        catch
        {
            f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        t.font = f;

        t.alignment = TextAnchor.UpperCenter;
        t.raycastTarget = false;
        t.fontSize = 28;
        t.text = prefix + "00:00";

        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(800f, 60f);

        countdownText = t;
    }
}
