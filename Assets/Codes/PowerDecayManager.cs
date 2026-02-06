using System.Collections.Generic;
using UnityEngine;


/// Global timer + difficulty scaling for power decay (now drives StreetLampPower).
/// Attach this to a single GameObject in the scene (e.g., CityGenerator).
public class PowerDecayManager : MonoBehaviour
{
    public static PowerDecayManager Instance { get; private set; }

    [Header("Session Timing")]
    [Tooltip("Total session length used to normalize the difficulty curve (e.g., 600 = 10 min).")]
    [Min(1f)] public float sessionLengthSeconds = 600f;

    [Tooltip("If false, timer stops and decay pauses.")]
    public bool isRunning = true;

    [Header("Difficulty / Decay Scaling")]
    [Tooltip("Decay multiplier over normalized time (0..1). Example: 1 at start -> 2 at end.")]
    public AnimationCurve decayMultiplierOverTime = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 2f)
    );

    public float ElapsedSeconds { get; private set; }

    private readonly List<StreetLampPower> _lamps = new List<StreetLampPower>();
    public IReadOnlyList<StreetLampPower> Lamps => _lamps;

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
        float t = sessionLengthSeconds <= 0f ? 1f : Mathf.Clamp01(ElapsedSeconds / sessionLengthSeconds);
        return Mathf.Max(0f, decayMultiplierOverTime.Evaluate(t));
    }

    public float GetAverageLampPower01()
    {
        if (_lamps.Count == 0) return 1f;

        float sum = 0f;
        int count = 0;
        foreach (var l in _lamps)
        {
            if (l == null) continue;
            sum += l.NormalizedPower01;
            count++;
        }
        return count == 0 ? 1f : Mathf.Clamp01(sum / count);
    }

    public void ResetTimer(bool runAfterReset = true)
    {
        ElapsedSeconds = 0f;
        isRunning = runAfterReset;
    }

    public void SetRunning(bool running) => isRunning = running;

    internal void Register(StreetLampPower lamp)
    {
        if (lamp == null) return;
        if (!_lamps.Contains(lamp)) _lamps.Add(lamp);
    }

    internal void Unregister(StreetLampPower lamp)
    {
        if (lamp == null) return;
        _lamps.Remove(lamp);
    }
}
