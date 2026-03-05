using System;
using System.Collections.Generic;
using UnityEngine;


/// Restores the ORIGINAL Main Menu look when the StartPanel becomes active again.
/// 
/// This version avoids disabling the whole CityGenerator GameObject (which can prevent generation),
/// and instead hides the previous run's world by disabling Renderers and Lights while in the menu.
/// It also force-hides gameplay panels like WinPanel, so their text and translucent backgrounds do not remain.
/// 
/// Attach this to the StartPanel root (the panel that is enabled when you go back to Main Menu).
/// No changes to GameUIController are required.
public class RestoreMenuVisualsOnEnable : MonoBehaviour
{
    [Header("Restore menu snapshot")]
    public bool restoreSnapshot = true;

    [Header("Sunrise cleanup")]
    public bool stopAllSunriseEffects = true;
    public bool destroyRuntimeSuns = true;
    public bool destroySunriseOverlayObjects = true;

    [Header("Force-hide gameplay UI panels (Win/Lose/Pause/Gameplay/Credits)")]
    public bool forceHideGameplayPanels = true;

    [Header("Hide gameplay world behind the menu (recommended)")]
    public bool hideWorldBehindMenu = true;

    [Tooltip("Root object name that holds the procedural content, e.g., CityGenerator.")]
    public string cityGeneratorRootName = "CityGenerator";

    [Tooltip("Optional extra root name to hide, for example PlayerRoot, if it exists outside CityGenerator.")]
    public string playerRootName = "PlayerRoot";

    [Header("Third-party sky controllers (optional)")]
    public bool disableSkyControllersOnMenu = false;

    [Tooltip("Type name keywords. Components whose type name contains one of these will be disabled while in menu.")]
    public string[] skyControllerTypeKeywords = new string[]
    {
        "CartoonSky",
        "OldStyle",
        "TimeOfDay",
        "DayNight",
        "SkyController",
        "SkyManager"
    };

    [Header("Debug")]
    public bool logOnEnable = false;

    bool _menuCaptured;

    // Cached roots
    GameObject _cachedCityGenerator;
    GameObject _cachedPlayerRoot;

    // Things we disabled (so we can restore them when leaving menu)
    readonly List<Renderer> _disabledRenderers = new List<Renderer>();
    readonly List<Light> _disabledLights = new List<Light>();
    readonly List<MonoBehaviour> _disabledSkyBehaviours = new List<MonoBehaviour>();

    void Awake()
    {
        EnsureSnapshotCaptured();
        CacheRoots();
    }

    void OnEnable()
    {
        EnsureSnapshotCaptured();
        CacheRoots();

        if (logOnEnable)
            Debug.Log("[RestoreMenuVisualsOnEnable] StartPanel enabled. Restoring menu visuals and hiding gameplay objects.");

        if (stopAllSunriseEffects)
            StopAndResetAllSunriseEffects();

        if (destroyRuntimeSuns)
            DestroyAllRuntimeSuns();

        if (destroySunriseOverlayObjects)
            DestroyAllSunriseOverlayObjects();

        if (forceHideGameplayPanels)
            ForceHideGameplayPanelsByReference();

        if (hideWorldBehindMenu)
            SetWorldVisibility(false);

        if (disableSkyControllersOnMenu)
            SetThirdPartySkyControllersEnabled(false);

        if (restoreSnapshot && MenuVisualSnapshot.Instance != null)
            MenuVisualSnapshot.Instance.Restore();
    }

    void OnDisable()
    {
        // Leaving menu: restore the world and sky controllers for gameplay.
        if (hideWorldBehindMenu)
            SetWorldVisibility(true);

        if (disableSkyControllersOnMenu)
            SetThirdPartySkyControllersEnabled(true);
    }

    void CacheRoots()
    {
        if (_cachedCityGenerator == null && !string.IsNullOrEmpty(cityGeneratorRootName))
        {
            // Important: GameObject.Find only finds active objects.
            // If it isn't found, we will still be okay because the world might not exist yet.
            var go = GameObject.Find(cityGeneratorRootName);
            if (go != null) _cachedCityGenerator = go;
        }

        if (_cachedPlayerRoot == null && !string.IsNullOrEmpty(playerRootName))
        {
            if (_cachedCityGenerator != null)
            {
                var t = FindChildByName(_cachedCityGenerator.transform, playerRootName);
                if (t != null) _cachedPlayerRoot = t.gameObject;
            }

            if (_cachedPlayerRoot == null)
            {
                var go = GameObject.Find(playerRootName);
                if (go != null) _cachedPlayerRoot = go;
            }
        }
    }

    void EnsureSnapshotCaptured()
    {
        if (_menuCaptured) return;

        if (MenuVisualSnapshot.Instance == null)
        {
            var snap = gameObject.GetComponent<MenuVisualSnapshot>();
            if (snap == null) snap = gameObject.AddComponent<MenuVisualSnapshot>();
            snap.captureOnAwake = false;
            snap.Capture();
        }
        else
        {
            MenuVisualSnapshot.Instance.Capture();
        }

        _menuCaptured = true;
    }

    void ForceHideGameplayPanelsByReference()
    {
#if UNITY_2023_1_OR_NEWER
        var ui = UnityEngine.Object.FindFirstObjectByType<GameUIController>(FindObjectsInactive.Include);
#else
        var ui = UnityEngine.Object.FindObjectOfType<GameUIController>(true);
#endif
        if (ui == null) return;

        // We want ONLY the start panel visible when we're in the menu.
        if (ui.winPanel != null) ui.winPanel.SetActive(false);
        if (ui.losePanel != null) ui.losePanel.SetActive(false);
        if (ui.pausePanel != null) ui.pausePanel.SetActive(false);
        if (ui.gamePlayPanel != null) ui.gamePlayPanel.SetActive(false);
        if (ui.creditsPanel != null) ui.creditsPanel.SetActive(false);
        // try
        // {
        //     if (ui.RepairHintPopup != null) ui.RepairHintPopup.SetActive(false);
        // }
        // catch
        // {
        //     
        // }

        // ui.startPanel is already active because this script lives on it, but keep safe:
        if (ui.startPanel != null) ui.startPanel.SetActive(true);
    }

    void SetWorldVisibility(bool visible)
    {
        if (visible)
        {
            // Restore only things we disabled ourselves.
            for (int i = 0; i < _disabledRenderers.Count; i++)
            {
                var r = _disabledRenderers[i];
                if (r != null) r.enabled = true;
            }
            for (int i = 0; i < _disabledLights.Count; i++)
            {
                var l = _disabledLights[i];
                if (l != null) l.enabled = true;
            }

            _disabledRenderers.Clear();
            _disabledLights.Clear();
            return;
        }

        // Hide: disable renderers and lights under CityGenerator and PlayerRoot.
        DisableWorldUnderRoot(_cachedCityGenerator);
        DisableWorldUnderRoot(_cachedPlayerRoot);
    }

    void DisableWorldUnderRoot(GameObject root)
    {
        if (root == null) return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!r.enabled) continue;

            r.enabled = false;
            _disabledRenderers.Add(r);
        }

        var lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;
            if (!l.enabled) continue;

            l.enabled = false;
            _disabledLights.Add(l);
        }
    }

    void SetThirdPartySkyControllersEnabled(bool enabled)
    {
        if (skyControllerTypeKeywords == null || skyControllerTypeKeywords.Length == 0) return;

        if (enabled)
        {
            // Restore only behaviours we disabled ourselves.
            for (int i = 0; i < _disabledSkyBehaviours.Count; i++)
            {
                var b = _disabledSkyBehaviours[i];
                if (b != null) b.enabled = true;
            }
            _disabledSkyBehaviours.Clear();
            return;
        }

#if UNITY_2023_1_OR_NEWER
        var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
#endif
        for (int i = 0; i < behaviours.Length; i++)
        {
            var b = behaviours[i];
            if (b == null) continue;
            if (!b.enabled) continue;

            string tn = b.GetType().Name;
            if (string.IsNullOrEmpty(tn)) continue;

            for (int k = 0; k < skyControllerTypeKeywords.Length; k++)
            {
                string kw = skyControllerTypeKeywords[k];
                if (string.IsNullOrEmpty(kw)) continue;

                if (tn.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    b.enabled = false;
                    _disabledSkyBehaviours.Add(b);
                    break;
                }
            }
        }
    }

    static void StopAndResetAllSunriseEffects()
    {
#if UNITY_2023_1_OR_NEWER
        var effects = UnityEngine.Object.FindObjectsByType<SunriseFinaleEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var effects = UnityEngine.Object.FindObjectsOfType<SunriseFinaleEffect>(true);
#endif
        for (int i = 0; i < effects.Length; i++)
        {
            var e = effects[i];
            if (e == null) continue;
            e.StopAndReset(true);
        }
    }

    static void DestroyAllRuntimeSuns()
    {
#if UNITY_2023_1_OR_NEWER
        var pins = UnityEngine.Object.FindObjectsByType<SunPinnedToScreen3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var pins = UnityEngine.Object.FindObjectsOfType<SunPinnedToScreen3D>(true);
#endif
        for (int i = 0; i < pins.Length; i++)
        {
            var p = pins[i];
            if (p == null) continue;
            UnityEngine.Object.Destroy(p.gameObject);
        }

#if UNITY_2023_1_OR_NEWER
        var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null) continue;
            string n = go.name;
            if (string.IsNullOrEmpty(n)) continue;

            if (n.Contains("_RuntimeSun") || n.Contains("RuntimeSun"))
                UnityEngine.Object.Destroy(go);
        }
    }

    static void DestroyAllSunriseOverlayObjects()
    {
#if UNITY_2023_1_OR_NEWER
        var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null) continue;

            string n = go.name;
            if (string.IsNullOrEmpty(n)) continue;

            if (n == "SunriseOverlay" || n == "SunriseOverlayCanvas" || n.Contains("SunriseOverlay"))
                UnityEngine.Object.Destroy(go);
        }
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c == null) continue;

            if (string.Equals(c.name, childName, StringComparison.OrdinalIgnoreCase))
                return c;

            var nested = FindChildByName(c, childName);
            if (nested != null) return nested;
        }
        return null;
    }
}
