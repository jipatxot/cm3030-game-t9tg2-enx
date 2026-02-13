using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PowerRepairInteraction : MonoBehaviour
{
    public Key repairKey = Key.A;

    [Header("Repair Behavior")]
    public bool repairAll = true;
    public bool onlyDark = false;

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

            if (onlyDark && d.CurrentPower > 0.001f) continue;

            d.RestoreToFull();
            TryRelightLightSource(d);

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

            if (onlyDark && d.CurrentPower > 0.001f) continue;

            d.RestoreToFull();
            TryRelightLightSource(d);

            if (logRepairedNames)
                Debug.Log($"[PowerRepairInteraction] -> {d.gameObject.name} (activeInHierarchy={d.gameObject.activeInHierarchy})");

            return 1;
        }

        return 0;
    }

    /// <summary>
    /// IMPORTANT: Your StreetLamp objects also get a LightSource component at runtime
    /// (added by StreetFurnitureGenerator). LightSource can disable Light components on its own timer.
    /// When repairing, we also force LightSource to "lit" so StreetLamps reliably relight like TrafficLights.
    /// </summary>
    private static void TryRelightLightSource(LightPowerDecay d)
    {
        if (d == null) return;

        var src = d.GetComponent<LightSource>();
        if (src != null)
        {
            // triggeredByPlayer=false so it won't heal player, just relight/reset its internal timer
            src.SetLit(true, false);
        }
    }

    /// <summary>
    /// Use a scene scan (including inactive) so we don't miss objects that are not registered
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
