using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prevents the gameplay world (generated city and player) from showing behind menu panels.
/// 
/// It hides Renderers and Lights under CityGenerator and PlayerRoot while the user is in menu context:
/// - StartPanel, SettingsPanel, HelpPanel
/// - CreditsPanel only when it was opened from the menu (tracked by CreditsPanelRouter)
/// 
/// This avoids disabling CityGenerator, so it will not break procedural generation.
/// Attach this to a persistent object, such as the same GameObject as GameUIController.
/// </summary>
public class MenuWorldBackdropController : MonoBehaviour
{
    [Header("World roots")]
    public string cityGeneratorRootName = "CityGenerator";
    public string playerRootName = "PlayerRoot";

    [Header("Menu panels (names)")]
    public string startPanelName = "StartPanel";
    public string settingsPanelName = "SettingsPanel";
    public string helpPanelName = "HelpPanel";
    public string creditsPanelName = "CreditsPanel";

    [Header("Update")]
    [Min(0.05f)] public float rescanInterval = 0.35f;

    [Header("Debug")]
    public bool logStateChanges = false;

    GameObject _cityRoot;
    GameObject _playerRoot;

    GameObject _startPanel;
    GameObject _settingsPanel;
    GameObject _helpPanel;
    GameObject _creditsPanel;

    float _timer;
    bool _worldHidden;

    readonly HashSet<Renderer> _disabledRenderers = new HashSet<Renderer>();
    readonly HashSet<Light> _disabledLights = new HashSet<Light>();

    void Start()
    {
        CacheReferences();
    }

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < rescanInterval) return;
        _timer = 0f;

        CacheReferences();

        bool inMenu = IsInMenuContext();

        if (inMenu && !_worldHidden)
        {
            HideWorld();
            _worldHidden = true;
            if (logStateChanges) Debug.Log("[MenuWorldBackdropController] Hiding world for menu.");
        }
        else if (!inMenu && _worldHidden)
        {
            ShowWorld();
            _worldHidden = false;
            if (logStateChanges) Debug.Log("[MenuWorldBackdropController] Restoring world for gameplay.");
        }
        else if (inMenu && _worldHidden)
        {
            HideWorld();
        }
    }

    void CacheReferences()
    {
        if (_cityRoot == null) _cityRoot = FindGOByName(cityGeneratorRootName);
        if (_playerRoot == null) _playerRoot = FindGOByName(playerRootName);

        if (_startPanel == null) _startPanel = FindGOByName(startPanelName);
        if (_settingsPanel == null) _settingsPanel = FindGOByName(settingsPanelName);
        if (_helpPanel == null) _helpPanel = FindGOByName(helpPanelName);
        if (_creditsPanel == null) _creditsPanel = FindGOByName(creditsPanelName);
    }

    bool IsInMenuContext()
    {
        if (_startPanel != null && _startPanel.activeInHierarchy) return true;
        if (_settingsPanel != null && _settingsPanel.activeInHierarchy) return true;
        if (_helpPanel != null && _helpPanel.activeInHierarchy) return true;

        if (_creditsPanel != null && _creditsPanel.activeInHierarchy)
            return CreditsPanelRouter.CreditsOpenedFromMenu;

        return false;
    }

    void HideWorld()
    {
        DisableUnderRoot(_cityRoot);
        DisableUnderRoot(_playerRoot);
    }

    void DisableUnderRoot(GameObject root)
    {
        if (root == null) return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            if (r.enabled)
            {
                r.enabled = false;
                _disabledRenderers.Add(r);
            }
        }

        var lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;

            if (l.enabled)
            {
                l.enabled = false;
                _disabledLights.Add(l);
            }
        }
    }

    void ShowWorld()
    {
        foreach (var r in _disabledRenderers)
            if (r != null) r.enabled = true;

        foreach (var l in _disabledLights)
            if (l != null) l.enabled = true;

        _disabledRenderers.Clear();
        _disabledLights.Clear();
    }

    static GameObject FindGOByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

#if UNITY_2023_1_OR_NEWER
        var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null) continue;
            if (!go.scene.IsValid()) continue;

            if (string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase))
                return go;
        }

        return null;
    }
}
