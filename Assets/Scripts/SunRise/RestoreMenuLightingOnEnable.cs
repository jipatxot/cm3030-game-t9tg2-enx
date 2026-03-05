using UnityEngine;


/// Restores the captured "Main Menu" lighting state when the main menu panel becomes visible again.
/// 
/// Attach this to your StartPanel root (the panel GameUIController activates in ShowStartScreen()).
/// When StartPanel is enabled, this will:
/// - Stop and reset any SunriseFinaleEffect (remove overlay + destroy runtime sun)
/// - Restore menu lighting using MenuLightingSnapshot (ambient, fog, skybox, directional lights)
/// - Destroy any leftover runtime sun objects as a safety net
/// 
/// This requires NO changes to GameUIController.
public class RestoreMenuLightingOnEnable : MonoBehaviour
{
    [Header("Behavior")]
    public bool restoreLightingSnapshot = true;
    public bool stopSunriseEffects = true;
    public bool destroyRuntimeSunObjects = true;

    [Header("Debug")]
    public bool logOnEnable = false;

    void OnEnable()
    {
        if (logOnEnable)
            Debug.Log("[RestoreMenuLightingOnEnable] StartPanel enabled. Restoring menu visuals.");

        if (stopSunriseEffects)
            StopAllSunriseEffects();

        if (restoreLightingSnapshot && MenuLightingSnapshot.Instance != null)
            MenuLightingSnapshot.Instance.Restore();

        if (destroyRuntimeSunObjects)
            DestroyRuntimeSuns();
    }

    public void RestoreNow()
    {
        OnEnable();
    }

    static void StopAllSunriseEffects()
    {
#if UNITY_2023_1_OR_NEWER
        var effects = Object.FindObjectsByType<SunriseFinaleEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var effects = Object.FindObjectsOfType<SunriseFinaleEffect>(true);
#endif
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] == null) continue;
            effects[i].StopAndReset(true);
        }
    }

    static void DestroyRuntimeSuns()
    {
        // Safety net: destroy any runtime suns created by SunriseFinaleEffect
        // (it typically names them like "<PrefabName>_RuntimeSun")
#if UNITY_2023_1_OR_NEWER
        var pins = Object.FindObjectsByType<SunPinnedToScreen3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var pins = Object.FindObjectsOfType<SunPinnedToScreen3D>(true);
#endif
        for (int i = 0; i < pins.Length; i++)
        {
            var p = pins[i];
            if (p == null) continue;
            // Destroy the whole object that has the pin script (runtime instance)
            Object.Destroy(p.gameObject);
        }

        // Also destroy by name match just in case
        var all = Object.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null) continue;
            string n = go.name;
            if (n != null && n.Contains("RuntimeSun"))
                Object.Destroy(go);
        }
    }
}
