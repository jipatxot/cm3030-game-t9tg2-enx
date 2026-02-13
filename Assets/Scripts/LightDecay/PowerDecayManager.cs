using System.Collections.Generic;
using UnityEngine;


/// Global timer + difficulty scaling for power decay.
/// All LightPowerDecay instances query GetDecayMultiplier() each frame.
/// 
/// Difficulty is selected in the Inspector on the scene's PowerDecayManager.
/// Harder difficulty => decay speeds up sooner => blackout happens earlier.
public class PowerDecayManager : MonoBehaviour
{
    public static PowerDecayManager Instance { get; private set; }

    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    [Header("Difficulty")]
    public Difficulty difficulty = Difficulty.Normal;

    [Tooltip("How fast we progress through the decay curve. Higher => reaches late-game multiplier sooner.")]
    [Min(0.01f)] public float easyCurveSpeed = 0.75f;
    [Min(0.01f)] public float normalCurveSpeed = 1.00f;
    [Min(0.01f)] public float hardCurveSpeed = 1.35f;

    [Tooltip("Extra multiplier applied on top of the curve. Higher => globally faster drain.")]
    [Min(0f)] public float easyMultiplierScale = 0.85f;
    [Min(0f)] public float normalMultiplierScale = 1.00f;
    [Min(0f)] public float hardMultiplierScale = 1.20f;

    [Header("Session Timing")]
    [Tooltip("Baseline length used as the NORMAL difficulty reference (seconds).")]
    [Min(1f)] public float sessionLengthSeconds = 600f;

    [Tooltip("If false, timer stops and decay pauses.")]
    public bool isRunning = true;

    [Header("Difficulty / Decay Scaling")]
    [Tooltip("Multiplier over normalized progress (0..1). Example: 1 at start -> 2 at end.")]
    public AnimationCurve decayMultiplierOverTime =
        new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 2f)
        );

    public float ElapsedSeconds { get; private set; }

    private readonly List<LightPowerDecay> _devices = new List<LightPowerDecay>();

    public IReadOnlyList<LightPowerDecay> Devices => _devices;

    // Back-compat alias used by older scripts
    public IReadOnlyList<LightPowerDecay> Lamps => _devices;

    public float CurveSpeed
    {
        get
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return Mathf.Max(0.01f, easyCurveSpeed);
                case Difficulty.Hard: return Mathf.Max(0.01f, hardCurveSpeed);
                default: return Mathf.Max(0.01f, normalCurveSpeed);
            }
        }
    }

    public float MultiplierScale
    {
        get
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return Mathf.Max(0f, easyMultiplierScale);
                case Difficulty.Hard: return Mathf.Max(0f, hardMultiplierScale);
                default: return Mathf.Max(0f, normalMultiplierScale);
            }
        }
    }


    /// Effective session length after applying CurveSpeed.
    /// Higher CurveSpeed => shorter effective session => multiplier ramps up sooner.
    public float EffectiveSessionLengthSeconds => sessionLengthSeconds / CurveSpeed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!isRunning) return;
        ElapsedSeconds += Time.deltaTime;
    }


    /// Difficulty-scaled decay multiplier at the current time.
    public float GetDecayMultiplier()
    {
        float effLen = Mathf.Max(0.0001f, EffectiveSessionLengthSeconds);
        float t01 = Mathf.Clamp01(ElapsedSeconds / effLen);

        float curve = Mathf.Max(0f, decayMultiplierOverTime.Evaluate(t01));
        return MultiplierScale * curve;
    }


    /// Difficulty-scaled curve evaluation at normalized time (0..1).
    /// Used by "precise countdown" (integrates curve).
    public float EvaluateScaledCurve01(float t01)
    {
        float v = Mathf.Max(0f, decayMultiplierOverTime.Evaluate(Mathf.Clamp01(t01)));
        return MultiplierScale * v;
    }

    internal void Register(LightPowerDecay d)
    {
        if (d == null) return;
        if (!_devices.Contains(d)) _devices.Add(d);
    }

    internal void Unregister(LightPowerDecay d)
    {
        if (d == null) return;
        _devices.Remove(d);
    }
}
