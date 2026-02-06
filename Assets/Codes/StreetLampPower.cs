using UnityEngine;


/// Power decay component for StreetLamp prefab.
/// This script dims the *child* Light components gradually based on CurrentPower.
/// Attach it to the ROOT "StreetLamp" object in StreetLamp.prefab.
public class StreetLampPower : MonoBehaviour
{
    [Header("Power")]
    [Min(0.01f)] public float maxPower = 100f;

    [Tooltip("Power drain per second when multiplier = 1.")]
    [Min(0f)] public float baseDecayPerSecond = 2f;

    [Tooltip("If true, power is set to max on Start.")]
    public bool startFullyPowered = true;

    [Tooltip("Optional delay before decay starts (useful for tutorial pacing).")]
    [Min(0f)] public float decayStartDelaySeconds = 0f;

    [Tooltip("Optional grace period after repairing (decay paused briefly).")]
    [Min(0f)] public float graceAfterRepairSeconds = 0.5f;

    [Header("Lamp Brightness Mapping")]
    [Tooltip("If true, automatically finds Light components in children on Start.")]
    public bool autoFindChildLights = true;

    [Tooltip("Include inactive child lights when auto-finding.")]
    public bool includeInactiveLights = true;

    [Tooltip("Minimum brightness multiplier when power is 0 (0 = fully off).")]
    [Range(0f, 1f)] public float minBrightnessAtZero = 0f;

    [Tooltip("If true, disables Light components when brightness is ~0.")]
    public bool disableLightsWhenOff = true;

    [Header("Debug (Read Only)")]
    [SerializeField] private float debugCurrentPower;
    [SerializeField] private float debugNormalizedPower01;

    public float CurrentPower { get; private set; }
    public float NormalizedPower01 => maxPower <= 0f ? 0f : Mathf.Clamp01(CurrentPower / maxPower);

    // --- No System.Action<>: use delegates instead (no 'using System' needed) ---
    public delegate void PowerChangedEvent(StreetLampPower lamp, float currentPower, float normalized01);
    public delegate void LampEvent(StreetLampPower lamp);

    public event PowerChangedEvent OnPowerChanged;
    public event LampEvent OnBecameDark;
    public event LampEvent OnBecameLit;
    // -------------------------------------------------------------------------

    private float _delayTimer;
    private float _graceTimer;

    private Light[] _lights;
    private float[] _baseIntensities;

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

        if (autoFindChildLights)
            CacheChildLights();

        CurrentPower = startFullyPowered ? maxPower : Mathf.Clamp(CurrentPower, 0f, maxPower);
        ApplyBrightness(NormalizedPower01, forceEnableDisable: true);
        FirePowerChanged();
    }

    private void Update()
    {
        // Keep debug visible in Inspector
        debugCurrentPower = CurrentPower;
        debugNormalizedPower01 = NormalizedPower01;

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
            float n = NormalizedPower01;
            ApplyBrightness(n, forceEnableDisable: false);
            FirePowerChanged();

            if (prev > 0f && CurrentPower <= 0f)
                OnBecameDark?.Invoke(this);
        }
    }

    public void RestoreToFull()
    {
        bool wasDark = CurrentPower <= 0f;

        CurrentPower = maxPower;
        _graceTimer = graceAfterRepairSeconds;

        float n = NormalizedPower01;
        ApplyBrightness(n, forceEnableDisable: true);
        FirePowerChanged();

        if (wasDark) OnBecameLit?.Invoke(this);
    }

    public void AddPower(float amount)
    {
        if (amount <= 0f) return;
        bool wasDark = CurrentPower <= 0f;

        CurrentPower = Mathf.Clamp(CurrentPower + amount, 0f, maxPower);
        _graceTimer = graceAfterRepairSeconds;

        float n = NormalizedPower01;
        ApplyBrightness(n, forceEnableDisable: true);
        FirePowerChanged();

        if (wasDark && CurrentPower > 0f) OnBecameLit?.Invoke(this);
    }

    [ContextMenu("Cache Child Lights")]
    public void CacheChildLights()
    {
        _lights = GetComponentsInChildren<Light>(includeInactiveLights);
        _baseIntensities = (_lights == null) ? null : new float[_lights.Length];

        if (_lights != null)
        {
            for (int i = 0; i < _lights.Length; i++)
                _baseIntensities[i] = _lights[i] != null ? _lights[i].intensity : 0f;
        }
    }

    private void ApplyBrightness(float normalizedPower01, bool forceEnableDisable)
    {
        if (_lights == null || _lights.Length == 0)
        {
            if (autoFindChildLights) CacheChildLights();
            if (_lights == null || _lights.Length == 0) return;
        }

        float brightness = Mathf.Lerp(minBrightnessAtZero, 1f, Mathf.Clamp01(normalizedPower01));
        bool shouldEnable = brightness > 0.001f;

        for (int i = 0; i < _lights.Length; i++)
        {
            var l = _lights[i];
            if (l == null) continue;

            float baseI = (_baseIntensities != null && i < _baseIntensities.Length) ? _baseIntensities[i] : l.intensity;
            l.intensity = baseI * brightness;

            if (disableLightsWhenOff)
            {
                if (forceEnableDisable || (!l.enabled && shouldEnable) || (l.enabled && !shouldEnable))
                    l.enabled = shouldEnable;
            }
        }
    }

    private void FirePowerChanged()
    {
        OnPowerChanged?.Invoke(this, CurrentPower, NormalizedPower01);
    }
}
