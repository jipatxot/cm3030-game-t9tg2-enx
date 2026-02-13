using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Press a key (default A) to repair lights (restore power to full).
/// 
/// NOTE: Your teammate removed LightSource.cs and replaced it with SafeZoneRegistry/LampSafeZone,
/// so this script MUST NOT reference LightSource anymore.
/// Repair simply calls LightPowerDecay.RestoreToFull() which will also re-enable/dim lights correctly.
/// </summary>
public class PowerRepairInteraction : MonoBehaviour
{
    public Key repairKey = Key.A;

    [Header("Repair Behavior")]
    [Tooltip("If true, repairs ALL LightPowerDecay objects in the scene. If false, repairs the first match.")]
    public bool repairAll = true;

    [Tooltip("If true, only repairs lights that are currently 'dark' (power <= threshold).")]
    public bool onlyDark = false;

    [Min(0f)] public float darkThresholdPower = 0.001f;

    [Header("Debug")]
    public bool logRepairedNames = false;

    public delegate void RepairedEvent();
    public static event RepairedEvent OnRepaired;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (!kb[repairKey].wasPressedThisFrame) return;

        int repaired = repairAll ? RepairAll() : RepairOneAny();

        if (repaired > 0)
        {
            Debug.Log($"[PowerRepairInteraction] Repaired {repaired} light object(s).");
            OnRepaired?.Invoke();
        }
        else
        {
            Debug.Log("[PowerRepairInteraction] No light object repaired.");
        }
    }

    private int RepairAll()
    {
        var list = GetAllIncludingInactive();
        int count = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;

            if (onlyDark && d.CurrentPower > darkThresholdPower) continue;

            d.RestoreToFull();

            if (logRepairedNames)
                Debug.Log($"[PowerRepairInteraction] -> {d.gameObject.name} (activeInHierarchy={d.gameObject.activeInHierarchy})");

            count++;
        }

        return count;
    }

    private int RepairOneAny()
    {
        var list = GetAllIncludingInactive();

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;

            if (onlyDark && d.CurrentPower > darkThresholdPower) continue;

            d.RestoreToFull();

            if (logRepairedNames)
                Debug.Log($"[PowerRepairInteraction] -> {d.gameObject.name} (activeInHierarchy={d.gameObject.activeInHierarchy})");

            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Scene scan (including inactive) so we don't miss objects that are not registered
    /// in PowerDecayManager (e.g., if something got disabled/unregistered).
    /// This runs only when you press the key, so it won't impact performance.
    /// </summary>
    private static List<LightPowerDecay> GetAllIncludingInactive()
    {
#if UNITY_2023_1_OR_NEWER
        var found = Object.FindObjectsByType<LightPowerDecay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var found = Object.FindObjectsOfType<LightPowerDecay>(true);
#endif
        return new List<LightPowerDecay>(found);
    }
}
