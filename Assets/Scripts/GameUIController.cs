using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;

    [Header("Panels")]
    public GameObject startPanel;
    public GameObject pausePanel;

    [Header("Start UI")]
    public TMP_InputField seedInput;
    public Button startButton;
    public Button quitButton;

    [Header("Pause UI")]
    public Button resumeButton;
    public Button exitButton;

    bool isPaused;
    bool gameStarted;

    void Awake()
    {
        Time.timeScale = 1f;
        isPaused = false;
        gameStarted = false;

        if (roads == null)
            roads = FindFirstObjectByType<RoadPathGenerator>();

        if (startButton != null) startButton.onClick.AddListener(OnStartGameClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnQuitClicked);

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
    }

    void ShowStartScreen()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);

        isPaused = false;
        gameStarted = false;
        Time.timeScale = 1f;
    }

    void ShowGameHudOnly()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        isPaused = false;
        gameStarted = true;
        Time.timeScale = 1f;
    }

    void OnStartGameClicked()
    {
        if (roads == null)
        {
            UnityEngine.Debug.LogError("RoadPathGenerator reference is missing on GameUIController.");
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
                UnityEngine.Application.Quit();
        #endif
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null) pausePanel.SetActive(true);
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
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
