using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PowerRepairInteraction : MonoBehaviour
{
    public Key repairKey = Key.A;

    public bool repairAll = true;
    public bool onlyDark = false;

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
            Debug.Log($"[PowerRepairInteraction] Repaired {repaired} light objects.");
            OnRepaired?.Invoke();
        }
        else
        {
            Debug.Log("[PowerRepairInteraction] No light object repaired.");
        }
    }

    private int RepairAll()
    {
        var list = GetAll();
        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;
            if (onlyDark && d.CurrentPower > 0.001f) continue;
            d.RestoreToFull();
            count++;
        }
        return count;
    }

    private int RepairOneAny()
    {
        var list = GetAll();
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;
            if (onlyDark && d.CurrentPower > 0.001f) continue;
            d.RestoreToFull();
            return 1;
        }
        return 0;
    }

    private static List<LightPowerDecay> GetAll()
    {
        if (PowerDecayManager.Instance != null && PowerDecayManager.Instance.Devices != null)
            return new List<LightPowerDecay>(PowerDecayManager.Instance.Devices);

#if UNITY_2023_1_OR_NEWER
        return new List<LightPowerDecay>(
            Object.FindObjectsByType<LightPowerDecay>(FindObjectsSortMode.None)
        );
#else
        return new List<LightPowerDecay>(Object.FindObjectsOfType<LightPowerDecay>());
#endif
    }
}
