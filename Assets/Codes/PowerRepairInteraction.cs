using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


/// Press A to restore StreetLamp brightness (power).
/// Attach to any always-active GameObject (e.g., CityGenerator).
public class PowerRepairInteraction : MonoBehaviour
{
    [Header("Input")]
    public Key repairKey = Key.A;

    [Header("Repair Behavior")]
    [Tooltip("If true, repairs every lamp at once when the key is pressed.")]
    public bool repairAllLamps = true;

    [Tooltip("If true, only repairs lamps that are currently dark/empty power.")]
    public bool onlyDarkLamps = false;

    
    
    public delegate void RepairedEvent();
    public static event RepairedEvent OnRepaired;

    
    
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (!kb[repairKey].wasPressedThisFrame) return;

        int repaired = repairAllLamps ? RepairAll() : RepairOneAny();

        if (repaired > 0)
        {
            Debug.Log($"[PowerRepairInteraction] Repaired {repaired} lamp(s).");
            OnRepaired?.Invoke();
        }

        else
            Debug.Log("[PowerRepairInteraction] No lamp repaired (none found / all already lit).");
    }

    private int RepairAll()
    {
        var lamps = GetLamps();
        int count = 0;

        for (int i = 0; i < lamps.Count; i++)
        {
            var l = lamps[i];
            if (l == null) continue;

            if (onlyDarkLamps && l.CurrentPower > 0f)
                continue;

            l.RestoreToFull();
            count++;
        }

        return count;
    }

    private int RepairOneAny()
    {
        var lamps = GetLamps();

        for (int i = 0; i < lamps.Count; i++)
        {
            var l = lamps[i];
            if (l == null) continue;

            if (onlyDarkLamps && l.CurrentPower > 0f)
                continue;

            l.RestoreToFull();
            return 1;
        }

        return 0;
    }

    private static List<StreetLampPower> GetLamps()
    {
        if (PowerDecayManager.Instance != null && PowerDecayManager.Instance.Lamps != null)
        {
            var list = new List<StreetLampPower>(PowerDecayManager.Instance.Lamps.Count);
            for (int i = 0; i < PowerDecayManager.Instance.Lamps.Count; i++)
            {
                var l = PowerDecayManager.Instance.Lamps[i];
                if (l != null) list.Add(l);
            }
            return list;
        }

        // return new List<StreetLampPower>(Object.FindObjectsOfType<StreetLampPower>());
        // Assets\Codes\PowerRepairInteraction.cs(87,42): warning CS0618: 'Object.FindObjectsOfType<T>()' is obsolete: 'Object.FindObjectsOfType has been deprecated. Use Object.FindObjectsByType instead which lets you decide whether you need the results sorted or not. FindObjectsOfType sorts the results by InstanceID but if you do not need this using FindObjectSortMode.None is considerably faster.'
        
#if UNITY_2023_1_OR_NEWER
        return new List<StreetLampPower>(
            Object.FindObjectsByType<StreetLampPower>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        );
#else
    return new List<StreetLampPower>(Object.FindObjectsOfType<StreetLampPower>(true)); // includeInactive
#endif

    }
}
