using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;

    [Header("Panels")]
    public GameObject startPanel;
    public GameObject pausePanel;
    public GameObject gamePlayPanel;

    [Header("Start UI")]
    public TMP_InputField seedInput;
    public Button startButton;
    public Button quitButton;

    [Header("Pause UI")]
    public Button resumeButton;
    public Button exitButton;

    [Header("Win UI")]
    public GameObject winPanel;
    public Button exitToMenuButton;
    public PowerDecayManager powerManager;

    bool gameWon;
    bool isPaused;
    bool gameStarted;

    void Awake()
    {
        Time.timeScale = 1f;
        isPaused = false;
        gameStarted = false;
        gameWon = false;

        if (roads == null)
            roads = FindFirstObjectByType<RoadPathGenerator>();

        if (startButton != null) startButton.onClick.AddListener(OnStartGameClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnQuitClicked);

        if (powerManager == null)
            powerManager = PowerDecayManager.Instance;

        if (exitToMenuButton != null)
            exitToMenuButton.onClick.AddListener(ReturnToMenu);

        if (winPanel != null) winPanel.SetActive(false);

        ShowStartScreen();
    }

    void Update()
    {
        if (!gameStarted) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }

        // Win check: sunrise reached
        if (!gameWon)
        {
            if (powerManager == null) powerManager = PowerDecayManager.Instance;

            if (powerManager != null)
            {
                float session = powerManager.EffectiveSessionLengthSeconds;
                if (powerManager.ElapsedSeconds >= session)
                    WinGame();
            }
        }
    }

    void ShowStartScreen()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null) powerManager.isRunning = false;

        isPaused = false;
        gameStarted = false;
        gameWon = false;
        Time.timeScale = 1f;

        if (seedInput != null)
        {
            seedInput.interactable = true;
            seedInput.ActivateInputField();
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void ShowGameHudOnly()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);

        if (powerManager == null)
            powerManager = PowerDecayManager.Instance;

        if (powerManager != null)
            powerManager.isRunning = true;

        isPaused = false;
        gameStarted = true;
        gameWon = false;
        Time.timeScale = 1f;

        if (seedInput != null) seedInput.DeactivateInputField();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void OnStartGameClicked()
    {
        if (roads == null)
        {
            Debug.LogError("RoadPathGenerator reference is missing on GameUIController.");
            return;
        }

        int? seedOverride = ParseSeedOrNull();

        if (seedOverride.HasValue)
        {
            roads.useRandomSeed = false;
            roads.seed = Mathf.Max(1, seedOverride.Value);
        }
        else
        {
            roads.useRandomSeed = true;
            roads.seed = 0;
        }

        roads.GenerateAndSpawn();

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null) powerManager.ResetTimer(true);

        ShowGameHudOnly();
    }

    void OnResumeClicked()
    {
        ResumeGame();
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null) powerManager.isRunning = false;

        if (pausePanel != null) pausePanel.SetActive(true);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(false);

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void ResumeGame()
    {
        if (gameWon) return;

        isPaused = false;
        Time.timeScale = 1f;

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null) powerManager.isRunning = true;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(true);

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void WinGame()
    {
        if (gameWon) return;

        gameWon = true;
        isPaused = true;

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null) powerManager.isRunning = false;

        Time.timeScale = 0f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(false);

        ForceShowPanel(winPanel);

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void ForceShowPanel(GameObject panel)
    {
        if (panel == null) return;

        Transform p = panel.transform.parent;
        while (p != null)
        {
            if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
            p = p.parent;
        }

        panel.SetActive(true);
    }

    void ReturnToMenu()
    {
        Time.timeScale = 1f;
        ShowStartScreen();
    }

    int? ParseSeedOrNull()
    {
        if (seedInput == null) return null;

        string s = seedInput.text;
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (int.TryParse(s, out int seed))
            return seed;

        return null;
    }
}
