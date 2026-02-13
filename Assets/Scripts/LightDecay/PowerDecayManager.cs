using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("How long the session lasts until sunrise (seconds). Difficulty does NOT change this.")]
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

    // Session length is now fixed (decoupled from CurveSpeed).
    public float EffectiveSessionLengthSeconds => sessionLengthSeconds;

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

    public float GetDecayMultiplier()
    {
        float len = Mathf.Max(0.0001f, EffectiveSessionLengthSeconds);

        // Progress through the curve is still difficulty-scaled.
        // Higher CurveSpeed => we reach late-game multiplier sooner,
        // but sunrise time stays the same.
        float t01 = Mathf.Clamp01((ElapsedSeconds / len) * CurveSpeed);

        float curve = Mathf.Max(0f, decayMultiplierOverTime.Evaluate(t01));
        return MultiplierScale * curve;
    }

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

    public float GetAverageLampPower01()
    {
        if (_devices.Count == 0) return 1f;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < _devices.Count; i++)
        {
            var d = _devices[i];
            if (d == null) continue;
            if (!d.countsTowardBlackout) continue;

            sum += d.NormalizedPower01;
            count++;
        }

        return count == 0 ? 1f : Mathf.Clamp01(sum / count);
    }

    public float GetTimeRemainingToSunrise()
    {
        float len = Mathf.Max(0.0001f, EffectiveSessionLengthSeconds);
        return Mathf.Max(0f, len - ElapsedSeconds);
    }

    public float GetSessionDurationSeconds()
    {
        return EffectiveSessionLengthSeconds;
    }

    public void ResetTimer(bool runAfterReset = true)
    {
        ElapsedSeconds = 0f;
        isRunning = runAfterReset;
    }
}
