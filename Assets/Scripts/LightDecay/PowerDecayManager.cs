using System.Collections.Generic;
using UnityEngine;

public class PowerDecayManager : MonoBehaviour
{
    public static PowerDecayManager Instance { get; private set; }

    [Header("Session Timing")]
    [Min(1f)] public float sessionLengthSeconds = 600f;
    public bool isRunning = true;

    [Header("Difficulty / Decay Scaling")]
    public AnimationCurve decayMultiplierOverTime = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 2f)
    );

    public float ElapsedSeconds { get; private set; }

    private readonly List<LightPowerDecay> _devices = new List<LightPowerDecay>();

    //
    public IReadOnlyList<LightPowerDecay> Devices => _devices;

    // 
    public IReadOnlyList<LightPowerDecay> Lamps => _devices;

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
        float t = Mathf.Clamp01(ElapsedSeconds / Mathf.Max(0.0001f, sessionLengthSeconds));
        return Mathf.Max(0f, decayMultiplierOverTime.Evaluate(t));
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