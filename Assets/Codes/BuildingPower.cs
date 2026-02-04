

using UnityEngine;

/// <summary>
/// Per-building power storage + decay over time.
/// Attach this to each Building GameObject.
/// </summary>
public class BuildingPower : MonoBehaviour
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

    [Header("Lighting / Visuals")]
    [Tooltip("Drag any light-related components here (e.g., Light2D, Light, SpriteRenderer glow).")]
    public Behaviour[] lightBehaviours;

    [Tooltip("Optional: objects to enable/disable (e.g., emissive sprites, light cone).")]
    public GameObject[] lightObjects;

    [Header("Low Power Feedback (optional)")]
    [Range(0f, 1f)] public float lowPowerThreshold01 = 0.2f;

    public float CurrentPower { get; private set; }
    public bool IsLit { get; private set; }

    /// <summary>(this, currentPower, normalized01)</summary>
    public event Action<BuildingPower, float, float> OnPowerChanged;

    /// <summary>Triggered when power hits 0 and building becomes dark.</summary>
    public event Action<BuildingPower> OnBecameDark;

    /// <summary>Triggered when building becomes lit again (power > 0).</summary>
    public event Action<BuildingPower> OnBecameLit;

    private float _delayTimer;
    private float _graceTimer;

    public float NormalizedPower01 => maxPower <= 0f ? 0f : Mathf.Clamp01(CurrentPower / maxPower);
    public bool IsLowPower => NormalizedPower01 <= lowPowerThreshold01;

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

        CurrentPower = startFullyPowered ? maxPower : Mathf.Clamp(CurrentPower, 0f, maxPower);
        SetLitState(CurrentPower > 0f, force: true);
        FirePowerChanged();
    }

    private void Update()
    {
        // Global state check
        float multiplier = 1f;
        if (PowerDecayManager.Instance != null)
        {
            if (!PowerDecayManager.Instance.isRunning) return;
            multiplier = PowerDecayManager.Instance.GetDecayMultiplier();
        }

        // Delay before starting decay
        if (_delayTimer > 0f)
        {
            _delayTimer -= Time.deltaTime;
            return;
        }

        // Grace after repair
        if (_graceTimer > 0f)
        {
            _graceTimer -= Time.deltaTime;
            return;
        }

        if (CurrentPower <= 0f || baseDecayPerSecond <= 0f) return;

        float drain = baseDecayPerSecond * multiplier * Time.deltaTime;
        if (drain <= 0f) return;

        float prev = CurrentPower;
        CurrentPower = Mathf.Max(0f, CurrentPower - drain);

        if (!Mathf.Approximately(prev, CurrentPower))
        {
            FirePowerChanged();

            // Just became dark
            if (prev > 0f && CurrentPower <= 0f)
            {
                SetLitState(false, force: false);
                OnBecameDark?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Call this from your Interaction system when the player repairs this building.
    /// Per design doc: Interaction restores power and immediately reactivates lighting. :contentReference[oaicite:4]{index=4} :contentReference[oaicite:5]{index=5}
    /// </summary>
    public void RestoreToFull()
    {
        bool wasDark = CurrentPower <= 0f;

        CurrentPower = maxPower;
        _graceTimer = graceAfterRepairSeconds;

        FirePowerChanged();

        SetLitState(true, force: false);
        if (wasDark) OnBecameLit?.Invoke(this);
    }

    /// <summary>
    /// Optional: partial repair / battery top-up.
    /// </summary>
    public void AddPower(float amount)
    {
        if (amount <= 0f) return;

        bool wasDark = CurrentPower <= 0f;

        CurrentPower = Mathf.Clamp(CurrentPower + amount, 0f, maxPower);
        _graceTimer = graceAfterRepairSeconds;

        FirePowerChanged();

        if (CurrentPower > 0f)
        {
            SetLitState(true, force: false);
            if (wasDark) OnBecameLit?.Invoke(this);
        }
    }

    private void SetLitState(bool lit, bool force)
    {
        if (!force && IsLit == lit) return;
        IsLit = lit;

        // Toggle components (Light2D/Light/etc. all inherit Behaviour)
        if (lightBehaviours != null)
        {
            foreach (var b in lightBehaviours)
            {
                if (b != null) b.enabled = lit;
            }
        }

        // Toggle objects
        if (lightObjects != null)
        {
            foreach (var go in lightObjects)
            {
                if (go != null) go.SetActive(lit);
            }
        }
    }

    private void FirePowerChanged()
    {
        OnPowerChanged?.Invoke(this, CurrentPower, NormalizedPower01);
    }
}
