using UnityEngine;

public class MenuPanelSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject helpPanel;

    public void OpenSettings()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (helpPanel != null) helpPanel.SetActive(false);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);
    }

    public void OpenHelp()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(true);
    }

    public void CloseHelp()
    {
        if (helpPanel != null) helpPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);
    }
}