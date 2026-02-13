using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Press a key (default F) to repair the nearest light within a radius around the player.
/// Repair calls LightPowerDecay.RestoreToFull().
/// </summary>
public class PowerRepairInteraction : MonoBehaviour
{
    [Header("Input")]
    public Key repairKey = Key.F;

    [Header("Repair Area")]
    public float repairRadius = 2.2f;

    [Header("Repair Timing")]
    [Tooltip("0 = instant. If > 0, waits then applies repair.")]
    public float repairDurationSeconds = 0f;

    [Header("Rules")]
    public bool onlyDark = false;
    [Min(0f)] public float darkThresholdPower = 0.001f;

    [Header("Debug")]
    public bool logRepairedNames = false;

    public delegate void RepairedEvent();
    public static event RepairedEvent OnRepaired;

    Transform player;
    Coroutine repairing;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (!kb[repairKey].wasPressedThisFrame) return;

        if (repairing != null) return;

        if (!TryGetPlayer(out var p)) return;

        var light = FindNearestInRadius(p.position, repairRadius);
        if (light == null)
        {
            Debug.Log("[PowerRepairInteraction] No light in range.");
            return;
        }

        if (onlyDark && light.CurrentPower > darkThresholdPower)
        {
            Debug.Log("[PowerRepairInteraction] Light already lit.");
            return;
        }

        if (repairDurationSeconds <= 0f)
        {
            light.RestoreToFull();
            if (logRepairedNames) Debug.Log($"[PowerRepairInteraction] -> {light.gameObject.name}");
            OnRepaired?.Invoke();
            return;
        }

        repairing = StartCoroutine(RepairAfterDelay(light, repairDurationSeconds));
    }

    IEnumerator RepairAfterDelay(LightPowerDecay light, float seconds)
    {
        float t = 0f;

        while (t < seconds)
        {
            if (light == null) { repairing = null; yield break; }
            t += Time.deltaTime;
            yield return null;
        }

        if (light != null)
        {
            light.RestoreToFull();
            if (logRepairedNames) Debug.Log($"[PowerRepairInteraction] -> {light.gameObject.name}");
            OnRepaired?.Invoke();
        }

        repairing = null;
    }

    bool TryGetPlayer(out Transform p)
    {
        if (player != null) { p = player; return true; }

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) { p = null; return false; }

        player = go.transform;
        p = player;
        return true;
    }

    LightPowerDecay FindNearestInRadius(Vector3 center, float radius)
    {
        var list = GetAllIncludingInactive();
        float r2 = radius * radius;

        LightPowerDecay best = null;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;

            Vector3 pos = d.transform.position;
            pos.y = center.y;

            float d2 = (pos - center).sqrMagnitude;
            if (d2 > r2) continue;

            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = d;
            }
        }

        return best;
    }

    static List<LightPowerDecay> GetAllIncludingInactive()
    {
#if UNITY_2023_1_OR_NEWER
        var found = Object.FindObjectsByType<LightPowerDecay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var found = Object.FindObjectsOfType<LightPowerDecay>(true);
#endif
        return new List<LightPowerDecay>(found);
    }
}
