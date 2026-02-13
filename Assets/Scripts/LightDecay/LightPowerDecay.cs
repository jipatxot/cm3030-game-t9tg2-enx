using UnityEngine;


/// Universal power-decay + dimming for StreetLamp / TrafficLight.
/// Attach to the ROOT object of the prefab (StreetLamp root / TrafficLight root).
///
/// It dims:
/// - Built-in Unity Light components (3D)
/// - (Optional) URP Light2D (via reflection, no hard dependency)
/// - (Optional) SpriteRenderers (multiply color brightness)
///
/// New: per-object variation ("stagger") so different lights can turn off at different times,
/// while still sharing one script. Configure ranges on each prefab.
public class LightPowerDecay : MonoBehaviour
{
    [Header("Power (Base)")]
    [Min(0.01f)] public float maxPower = 100f;
    [Min(0f)] public float baseDecayPerSecond = 2f;
    public bool startFullyPowered = true;

    [Min(0f)] public float decayStartDelaySeconds = 0f;
    [Min(0f)] public float graceAfterRepairSeconds = 0.5f;

    [Header("Variation / Stagger (Optional)")]
    [Tooltip("If enabled, each instance gets its own modifiers so they don't all turn off together.")]
    public bool enableVariation = true;

    [Tooltip("Deterministic = stable per position/name; RandomPerRun = different each play session.")]
    public SeedSource seedSource = SeedSource.Position;

    [Tooltip("Extra seed to change the overall pattern without changing prefab positions.")]
    public int variationSeed = 12345;

    [Tooltip("Extra delay added ON TOP of decayStartDelaySeconds (seconds).")]
    public Vector2 additionalStartDelayRange = new Vector2(0f, 8f);

    [Tooltip("Multiply maxPower per instance (capacity). 1..1 means no variation.")]
    public Vector2 capacityScaleRange = new Vector2(1f, 1f);

    [Tooltip("Multiply baseDecayPerSecond per instance (decay speed).")]
    public Vector2 decayScaleRange = new Vector2(0.8f, 1.2f);

    [Tooltip("If true, starting power is also scaled (useful so some lights start 'weaker').")]
    public bool scaleStartingPower = false;

    [Tooltip("Starting power scale (applies only if scaleStartingPower=true).")]
    public Vector2 startingPowerScaleRange = new Vector2(0.6f, 1.0f);

    public enum SeedSource
    {
        Position,
        Name,
        SiblingIndex,
        RandomPerRun
    }

    [Header("What to dim")]
    public bool autoFindUnityLights = true;
    public bool autoFindURPLight2D = true;
    public bool autoFindSpriteRenderers = false;

    [Tooltip("Also search inactive children when auto-finding.")]
    public bool includeInactiveChildren = true;

    [Header("Brightness Mapping")]
    [Range(0f, 1f)] public float minBrightnessAtZero = 0f;
    public bool disableWhenOff = false;

    [Header("Optional")]
    [Tooltip("If false, this object does NOT count toward blackout countdown (if you compute one).")]
    public bool countsTowardBlackout = true;

    [Header("Debug (Read Only)")]
    [SerializeField] private float debugCurrentPower;
    [SerializeField] private float debugNormalizedPower01;
    [SerializeField] private float debugCapacityScale = 1f;
    [SerializeField] private float debugDecayScale = 1f;
    [SerializeField] private float debugExtraStartDelay = 0f;

    public float CurrentPower { get; private set; }

    // Effective values after variation
    public float CapacityScale => debugCapacityScale;
    public float DecayScale => debugDecayScale;
    public float ExtraStartDelaySeconds => debugExtraStartDelay;

    public float EffectiveMaxPower => Mathf.Max(0.0001f, maxPower) * CapacityScale;
    public float EffectiveDecayPerSecond => Mathf.Max(0f, baseDecayPerSecond) * DecayScale;

    public float NormalizedPower01 => Mathf.Clamp01(CurrentPower / EffectiveMaxPower);


    /// Remaining seconds where this light will NOT decay (due to initial delay and/or grace after repair).
    /// Note: in this script delay and grace are sequential (delay blocks grace countdown), so we sum them.
    public float RemainingNoDecaySeconds => Mathf.Max(0f, _delayTimer) + Mathf.Max(0f, _graceTimer);

    // No System.Action<>: use delegates (no 'using System' needed)
    public delegate void PowerChangedEvent(LightPowerDecay item, float currentPower, float normalized01);
    public static event PowerChangedEvent OnAnyPowerChanged;

    private float _delayTimer;
    private float _graceTimer;

    // per-instance variation
    private uint _rngState = 1;

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
        CacheTargets();
        InitializeVariation();

        _delayTimer = decayStartDelaySeconds + ExtraStartDelaySeconds;

        float cap = EffectiveMaxPower;

        if (startFullyPowered)
        {
            CurrentPower = cap;
            if (enableVariation && scaleStartingPower)
            {
                float sp = LerpRange(startingPowerScaleRange, Next01());
                CurrentPower = Mathf.Clamp(cap * sp, 0f, cap);
            }
        }
        else
        {
            CurrentPower = Mathf.Clamp(CurrentPower, 0f, cap);
        }

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

        // Initial delay (blocks grace countdown too)
        if (_delayTimer > 0f) { _delayTimer -= Time.deltaTime; return; }

        // Grace after repair
        if (_graceTimer > 0f) { _graceTimer -= Time.deltaTime; return; }

        float decay = EffectiveDecayPerSecond;
        if (CurrentPower <= 0f || decay <= 0f) return;

        float drain = decay * multiplier * Time.deltaTime;
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
        CurrentPower = EffectiveMaxPower;
        _graceTimer = graceAfterRepairSeconds;

        ApplyBrightness(NormalizedPower01, forceEnableDisable: true);
        PushDebug();
        OnAnyPowerChanged?.Invoke(this, CurrentPower, NormalizedPower01);
    }

    public void AddPower(float amount)
    {
        if (amount <= 0f) return;

        float cap = EffectiveMaxPower;
        CurrentPower = Mathf.Clamp(CurrentPower + amount, 0f, cap);
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
        // debugCapacityScale/DecayScale/ExtraStartDelay are set in InitializeVariation()
    }

    private void InitializeVariation()
    {
        debugCapacityScale = 1f;
        debugDecayScale = 1f;
        debugExtraStartDelay = 0f;

        if (!enableVariation)
            return;

        int seed = ComputeSeed();
        if (seed == 0) seed = 1;

        _rngState = (uint)seed;

        debugExtraStartDelay = LerpRange(additionalStartDelayRange, Next01());
        debugCapacityScale = Mathf.Max(0.01f, LerpRange(capacityScaleRange, Next01()));
        debugDecayScale = Mathf.Max(0f, LerpRange(decayScaleRange, Next01()));
    }

    private int ComputeSeed()
    {
        unchecked
        {
            int h = variationSeed;

            if (seedSource == SeedSource.RandomPerRun)
            {
                // Different each play session
                h = h * 31 + Random.Range(int.MinValue, int.MaxValue);
                return h;
            }

            // Position hash (quantized)
            if (seedSource == SeedSource.Position)
            {
                Vector3 p = transform.position;
                int px = Mathf.RoundToInt(p.x * 1000f);
                int py = Mathf.RoundToInt(p.y * 1000f);
                int pz = Mathf.RoundToInt(p.z * 1000f);
                h = h * 31 + px;
                h = h * 31 + py;
                h = h * 31 + pz;
            }
            else if (seedSource == SeedSource.SiblingIndex)
            {
                h = h * 31 + transform.GetSiblingIndex();
            }
            else if (seedSource == SeedSource.Name)
            {
                h = h * 31 + StableStringHash(gameObject.name);
            }

            // Also mix in scale a bit (helps if many lights share same position in prefab space)
            Vector3 s = transform.lossyScale;
            h = h * 31 + Mathf.RoundToInt(s.x * 1000f);
            h = h * 31 + Mathf.RoundToInt(s.z * 1000f);

            return h;
        }
    }

    private static int StableStringHash(string s)
    {
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < s.Length; i++)
                hash = hash * 31 + s[i];
            return hash;
        }
    }

    private float Next01()
    {
        // LCG
        _rngState = 1664525u * _rngState + 1013904223u;
        // 24-bit mantissa
        return (_rngState & 0x00FFFFFFu) / 16777216f;
    }

    private static float LerpRange(Vector2 range, float t01)
    {
        float a = range.x;
        float b = range.y;
        if (b < a) { float tmp = a; a = b; b = tmp; }
        return Mathf.Lerp(a, b, Mathf.Clamp01(t01));
    }
}
