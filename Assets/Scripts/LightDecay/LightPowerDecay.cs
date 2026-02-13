using UnityEngine;


/// Universal power-decay + dimming for StreetLamp / TrafficLight.
/// Attach to the ROOT object of the prefab (StreetLamp root / TrafficLight root).
/// It dims:
/// - Built-in Unity Light components (3D)
/// - (Optional) URP Light2D (via reflection, no hard dependency)
/// - (Optional) SpriteRenderers (multiply color brightness)
public class LightPowerDecay : MonoBehaviour
{
    [Header("Power")]
    [Min(0.01f)] public float maxPower = 100f;
    [Min(0f)] public float baseDecayPerSecond = 2f;
    public bool startFullyPowered = true;

    [Min(0f)] public float decayStartDelaySeconds = 0f;
    [Min(0f)] public float graceAfterRepairSeconds = 0.5f;

    [Header("What to dim")]
    public bool autoFindUnityLights = true;
    public bool autoFindURPLight2D = true;
    public bool autoFindSpriteRenderers = false;

    [Tooltip("Also search inactive children when auto-finding.")]
    public bool includeInactiveChildren = true;

    [Header("Brightness Mapping")]
    [Range(0f, 1f)] public float minBrightnessAtZero = 0f;
    public bool disableWhenOff = true;

    [Header("Optional")]
    [Tooltip("If false, this object does NOT count toward blackout countdown (if you compute one).")]
    public bool countsTowardBlackout = true;

    [Header("Debug (Read Only)")]
    [SerializeField] private float debugCurrentPower;
    [SerializeField] private float debugNormalizedPower01;

    public float CurrentPower { get; private set; }
    public float NormalizedPower01 => maxPower <= 0f ? 0f : Mathf.Clamp01(CurrentPower / maxPower);

    // No System.Action<>: use delegates (no 'using System' needed)
    public delegate void PowerChangedEvent(LightPowerDecay item, float currentPower, float normalized01);
    public static event PowerChangedEvent OnAnyPowerChanged;

    private float _delayTimer;
    private float _graceTimer;

    // Unity Lights
    private Light[] _unityLights;
    private float[] _unityBaseIntensities;

    // SpriteRenderers
    private SpriteRenderer[] _sprites;
    private Color[] _spriteBaseColors;

    // URP Light2D (reflection)
    private Component[] _light2Ds;
    private float[] _light2DBaseIntensities;
    private global::System.Reflection.PropertyInfo _light2DIntensityProp; // intensity property

    private void OnEnable()
    {
        if (PowerDecayManager.Instance != null)
            PowerDecayManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (PowerDecayManager.Instance != null)
            PowerDecayManager.Instance.Unregister(this);
    }

    private void Start()
    {
        _delayTimer = decayStartDelaySeconds;

        CacheTargets();

        CurrentPower = startFullyPowered ? maxPower : Mathf.Clamp(CurrentPower, 0f, maxPower);
        ApplyBrightness(NormalizedPower01, forceEnableDisable: true);
        PushDebug();
        OnAnyPowerChanged?.Invoke(this, CurrentPower, NormalizedPower01);
    }

    private void Update()
    {
        PushDebug();

        float multiplier = 1f;
        if (PowerDecayManager.Instance != null)
        {
            if (!PowerDecayManager.Instance.isRunning) return;
            multiplier = PowerDecayManager.Instance.GetDecayMultiplier();
        }

        if (_delayTimer > 0f) { _delayTimer -= Time.deltaTime; return; }
        if (_graceTimer > 0f) { _graceTimer -= Time.deltaTime; return; }

        if (CurrentPower <= 0f || baseDecayPerSecond <= 0f) return;

        float drain = baseDecayPerSecond * multiplier * Time.deltaTime;
        if (drain <= 0f) return;

        float prev = CurrentPower;
        CurrentPower = Mathf.Max(0f, CurrentPower - drain);

        if (!Mathf.Approximately(prev, CurrentPower))
        {
            ApplyBrightness(NormalizedPower01, forceEnableDisable: false);
            OnAnyPowerChanged?.Invoke(this, CurrentPower, NormalizedPower01);
        }
    }

    public void RestoreToFull()
    {
        CurrentPower = maxPower;
        _graceTimer = graceAfterRepairSeconds;

        ApplyBrightness(NormalizedPower01, forceEnableDisable: true);
        PushDebug();
        OnAnyPowerChanged?.Invoke(this, CurrentPower, NormalizedPower01);
    }

    public void AddPower(float amount)
    {
        if (amount <= 0f) return;
        CurrentPower = Mathf.Clamp(CurrentPower + amount, 0f, maxPower);
        _graceTimer = graceAfterRepairSeconds;

        ApplyBrightness(NormalizedPower01, forceEnableDisable: true);
        PushDebug();
        OnAnyPowerChanged?.Invoke(this, CurrentPower, NormalizedPower01);
    }

    [ContextMenu("Cache Targets")]
    public void CacheTargets()
    {
        // Unity Light
        if (autoFindUnityLights)
        {
            _unityLights = GetComponentsInChildren<Light>(includeInactiveChildren);
            _unityBaseIntensities = (_unityLights == null) ? null : new float[_unityLights.Length];
            if (_unityLights != null)
            {
                for (int i = 0; i < _unityLights.Length; i++)
                    _unityBaseIntensities[i] = _unityLights[i] != null ? _unityLights[i].intensity : 0f;
            }
        }

        // SpriteRenderers
        if (autoFindSpriteRenderers)
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>(includeInactiveChildren);
            _spriteBaseColors = (_sprites == null) ? null : new Color[_sprites.Length];
            if (_sprites != null)
            {
                for (int i = 0; i < _sprites.Length; i++)
                    _spriteBaseColors[i] = _sprites[i] != null ? _sprites[i].color : Color.white;
            }
        }

        // URP Light2D (reflection)
        if (autoFindURPLight2D)
        {
            CacheURPLight2D();
        }
    }

    private void ApplyBrightness(float normalizedPower01, bool forceEnableDisable)
    {
        float brightness = Mathf.Lerp(minBrightnessAtZero, 1f, Mathf.Clamp01(normalizedPower01));
        bool shouldEnable = brightness > 0.001f;

        // Unity Lights
        if (_unityLights != null)
        {
            for (int i = 0; i < _unityLights.Length; i++)
            {
                var l = _unityLights[i];
                if (l == null) continue;

                float baseI = (_unityBaseIntensities != null && i < _unityBaseIntensities.Length) ? _unityBaseIntensities[i] : l.intensity;
                l.intensity = baseI * brightness;

                if (disableWhenOff)
                {
                    if (forceEnableDisable || (l.enabled != shouldEnable))
                        l.enabled = shouldEnable;
                }
            }
        }

        // SpriteRenderers
        if (_sprites != null)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                var s = _sprites[i];
                if (s == null) continue;

                Color baseC = (_spriteBaseColors != null && i < _spriteBaseColors.Length) ? _spriteBaseColors[i] : s.color;
                // keep alpha, scale RGB
                Color c = new Color(baseC.r * brightness, baseC.g * brightness, baseC.b * brightness, baseC.a);
                s.color = c;
            }
        }

        // URP Light2D
        if (_light2Ds != null && _light2DIntensityProp != null)
        {
            for (int i = 0; i < _light2Ds.Length; i++)
            {
                var comp = _light2Ds[i];
                if (comp == null) continue;

                float baseI = (_light2DBaseIntensities != null && i < _light2DBaseIntensities.Length) ? _light2DBaseIntensities[i] : 1f;
                float newI = baseI * brightness;
                _light2DIntensityProp.SetValue(comp, newI, null);

                if (disableWhenOff)
                {
                    var b = comp as Behaviour;
                    if (b != null && (forceEnableDisable || (b.enabled != shouldEnable)))
                        b.enabled = shouldEnable;
                }
            }
        }
    }

    private void CacheURPLight2D()
    {
        _light2Ds = null;
        _light2DBaseIntensities = null;
        _light2DIntensityProp = null;

        // Try URP Light2D type
        var t = global::System.Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
        if (t == null) return;

        _light2DIntensityProp = t.GetProperty("intensity");
        if (_light2DIntensityProp == null) return;

        _light2Ds = GetComponentsInChildren(t, includeInactiveChildren);
        if (_light2Ds == null) return;

        _light2DBaseIntensities = new float[_light2Ds.Length];
        for (int i = 0; i < _light2Ds.Length; i++)
        {
            var comp = _light2Ds[i];
            if (comp == null) { _light2DBaseIntensities[i] = 0f; continue; }
            object v = _light2DIntensityProp.GetValue(comp, null);
            _light2DBaseIntensities[i] = (v is float f) ? f : 1f;
        }
    }

    private void PushDebug()
    {
        debugCurrentPower = CurrentPower;
        debugNormalizedPower01 = NormalizedPower01;
    }
}
