

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global timer + difficulty scaling for power decay.
/// Attach this to a single GameObject in the scene (e.g., "GameManager").
/// </summary>
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

    private readonly List<BuildingPower> _buildings = new List<BuildingPower>();
    public IReadOnlyList<BuildingPower> Buildings => _buildings;

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

    /// <summary>
    /// Returns the current decay multiplier based on elapsed time.
    /// </summary>
    public float GetDecayMultiplier()
    {
        float t = sessionLengthSeconds <= 0f ? 1f : Mathf.Clamp01(ElapsedSeconds / sessionLengthSeconds);
        return Mathf.Max(0f, decayMultiplierOverTime.Evaluate(t));
    }

    /// <summary>
    /// Average normalized power across all registered buildings (0..1).
    /// Handy for UI, pacing, etc.
    /// </summary>
    public float GetAverageCityPower01()
    {
        if (_buildings.Count == 0) return 1f;

        float sum = 0f;
        int count = 0;
        foreach (var b in _buildings)
        {
            if (b == null) continue;
            sum += b.NormalizedPower01;
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

    internal void Register(BuildingPower building)
    {
        if (building == null) return;
        if (!_buildings.Contains(building)) _buildings.Add(building);
    }

    internal void Unregister(BuildingPower building)
    {
        if (building == null) return;
        _buildings.Remove(building);
    }
}
