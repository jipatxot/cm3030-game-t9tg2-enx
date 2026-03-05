using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fixes "Credits button does nothing" by wiring Credits navigation at runtime,
/// even if Inspector references are missing.
/// 
/// It supports:
/// - PausePanel -> CreditsPanel
/// - LosePanel  -> CreditsPanel
/// - WinPanel   -> CreditsPanel (optional)
/// - Menu panels (Start/Settings/Help) -> CreditsPanel (optional)
/// 
/// It tries to find panels by GameObject name (including inactive) and then finds Buttons
/// whose label text contains "CREDITS". It also wires a "Back" button on CreditsPanel if present.
/// 
/// This script does NOT require modifying GameUIController.
/// </summary>
public class CreditsPanelRouter : MonoBehaviour
{
    public static bool CreditsOpenedFromMenu { get; private set; }

    [Header("Panel names (change if your names differ)")]
    public string startPanelName = "StartPanel";
    public string settingsPanelName = "SettingsPanel";
    public string helpPanelName = "HelpPanel";
    public string creditsPanelName = "CreditsPanel";
    public string pausePanelName = "PausePanel";
    public string losePanelName = "LosePanel";
    public string winPanelName = "WinPanel";
    public string gameplayPanelName = "GamePlayPanel";

    [Header("Button label keywords")]
    public string creditsKeyword = "CREDITS";
    public string backKeyword = "BACK";
    public string menuKeyword = "MENU";

    [Header("Debug")]
    public bool logWiring = false;

    GameObject _startPanel, _settingsPanel, _helpPanel, _creditsPanel, _pausePanel, _losePanel, _winPanel, _gameplayPanel;
    GameObject _returnPanel;

    bool _wired;

    void Awake()
    {
        TryWireOnce();
    }

    void OnEnable()
    {
        TryWireOnce();
    }

    void TryWireOnce()
    {
        if (_wired) return;

        CachePanels();

        if (_creditsPanel == null)
        {
            if (logWiring) Debug.LogWarning("[CreditsPanelRouter] CreditsPanel not found. Check creditsPanelName.");
            return;
        }

        WireCreditsButtonsOnPanel(_pausePanel);
        WireCreditsButtonsOnPanel(_losePanel);
        WireCreditsButtonsOnPanel(_winPanel);

        WireCreditsButtonsOnPanel(_startPanel);
        WireCreditsButtonsOnPanel(_settingsPanel);
        WireCreditsButtonsOnPanel(_helpPanel);

        WireBackButtonsOnCredits();

        _wired = true;

        if (logWiring) Debug.Log("[CreditsPanelRouter] Wiring completed.");
    }

    void CachePanels()
    {
        _startPanel = FindGOByName(startPanelName);
        _settingsPanel = FindGOByName(settingsPanelName);
        _helpPanel = FindGOByName(helpPanelName);
        _creditsPanel = FindGOByName(creditsPanelName);
        _pausePanel = FindGOByName(pausePanelName);
        _losePanel = FindGOByName(losePanelName);
        _winPanel = FindGOByName(winPanelName);
        _gameplayPanel = FindGOByName(gameplayPanelName);
    }

    void WireCreditsButtonsOnPanel(GameObject panel)
    {
        if (panel == null) return;

        var buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b == null) continue;

            if (!TryGetButtonLabel(b, out string label)) continue;
            if (label == null) continue;

            if (label.IndexOf(creditsKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                b.onClick.RemoveListener(OpenCreditsFromUnknown);
                b.onClick.AddListener(OpenCreditsFromUnknown);

                if (logWiring) Debug.Log($"[CreditsPanelRouter] Wired Credits button on {panel.name} (label='{label}').");
            }
        }
    }

    void WireBackButtonsOnCredits()
    {
        var buttons = _creditsPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b == null) continue;

            if (!TryGetButtonLabel(b, out string label)) continue;
            if (label == null) continue;

            bool looksLikeBack = label.IndexOf(backKeyword, StringComparison.OrdinalIgnoreCase) >= 0
                              || label.IndexOf(menuKeyword, StringComparison.OrdinalIgnoreCase) >= 0;

            if (looksLikeBack)
            {
                b.onClick.RemoveListener(CloseCredits);
                b.onClick.AddListener(CloseCredits);

                if (logWiring) Debug.Log($"[CreditsPanelRouter] Wired Back/Menu button on CreditsPanel (label='{label}').");
            }
        }
    }

    void OpenCreditsFromUnknown()
    {
        _returnPanel = GetActivePanel();
        CreditsOpenedFromMenu = IsMenuPanel(_returnPanel);

        if (_creditsPanel != null) _creditsPanel.SetActive(true);

        if (_pausePanel != null) _pausePanel.SetActive(false);
        if (_losePanel != null) _losePanel.SetActive(false);
        if (_winPanel != null) _winPanel.SetActive(false);

        if (CreditsOpenedFromMenu)
        {
            if (_startPanel != null) _startPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_helpPanel != null) _helpPanel.SetActive(false);
        }

        if (!CreditsOpenedFromMenu && _gameplayPanel != null)
            _gameplayPanel.SetActive(false);
    }

    void CloseCredits()
    {
        if (_creditsPanel != null) _creditsPanel.SetActive(false);

        if (_returnPanel != null)
            _returnPanel.SetActive(true);
        else if (_startPanel != null)
            _startPanel.SetActive(true);

        CreditsOpenedFromMenu = false;
        _returnPanel = null;
    }

    GameObject GetActivePanel()
    {
        if (_pausePanel != null && _pausePanel.activeInHierarchy) return _pausePanel;
        if (_losePanel != null && _losePanel.activeInHierarchy) return _losePanel;
        if (_winPanel != null && _winPanel.activeInHierarchy) return _winPanel;

        if (_settingsPanel != null && _settingsPanel.activeInHierarchy) return _settingsPanel;
        if (_helpPanel != null && _helpPanel.activeInHierarchy) return _helpPanel;
        if (_startPanel != null && _startPanel.activeInHierarchy) return _startPanel;

        if (_gameplayPanel != null && _gameplayPanel.activeInHierarchy) return _gameplayPanel;

        return null;
    }

    bool IsMenuPanel(GameObject panel)
    {
        if (panel == null) return false;
        return panel == _startPanel || panel == _settingsPanel || panel == _helpPanel || panel == _creditsPanel;
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

    static bool TryGetButtonLabel(Button b, out string label)
    {
        label = null;
        if (b == null) return false;

        var t = b.GetComponentInChildren<Text>(true);
        if (t != null && !string.IsNullOrEmpty(t.text))
        {
            label = t.text;
            return true;
        }

        var comps = b.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;

            var tn = c.GetType().Name;
            if (tn == "TMP_Text" || tn == "TextMeshProUGUI")
            {
                var prop = c.GetType().GetProperty("text");
                if (prop != null)
                {
                    var v = prop.GetValue(c, null) as string;
                    if (!string.IsNullOrEmpty(v))
                    {
                        label = v;
                        return true;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(b.gameObject.name))
        {
            label = b.gameObject.name;
            return true;
        }

        return false;
    }
}
