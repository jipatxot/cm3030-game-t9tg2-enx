using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

public class GameUIController : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;

    [Header("Player (runtime)")]
    public PlayerHealth playerHealth;

    [Header("Managers")]
    public PowerDecayManager powerManager;

    [Header("Panels")]
    public GameObject startPanel;
    public GameObject pausePanel;
    public GameObject gamePlayPanel;
    public GameObject winPanel;
    public GameObject losePanel;
    public GameObject creditsPanel;

    [Header("Start Panel UI")]
    public TMP_InputField seedInput;
    public Button startButton;
    public Button quitButton;
    public Button startCreditsButton;

    [Header("Difficulty UI (Start Panel)")]
    public Toggle lowToggle;
    public Toggle mediumToggle;
    public Toggle highToggle;
    public ToggleGroup difficultyToggleGroup; // optional
    public TMP_Text difficultyDescriptionText;

    [Header("Session Duration UI (Start Panel)")]
    public TMP_Dropdown sessionDurationDropdown;

    [Tooltip("Base session seconds used when dropdown uses x multipliers like x1.5.")]
    public float baseSessionSecondsForMultiplier = 600f;

    [Tooltip("Fallback session seconds if label parsing fails.")]
    public float fallbackSessionSeconds = 600f;

    [Header("Pause Panel UI")]
    public Button resumeButton;
    public Button pauseExitButton;
    public Button pauseCreditsButton;

    [Header("Win Panel UI")]
    public Button winExitToMenuButton;
    public Button winCreditsButton;

    [Header("Lose Panel UI")]
    public Button loseExitToMenuButton;
    public Button restartButton;
    public Button loseCreditsButton;

    [Header("Credits Panel UI")]
    public Button creditsBackButton;

    [Header("Hard Pause Extras")]
    public bool pauseAudio = true;
    public bool pauseNavMeshAgents = true;

    [Header("Debug")]
    public bool logDifficultyApply = false;

    [System.Serializable]
    public class DifficultyConfig
    {
        [TextArea(2, 6)] public string description;

        [Header("Lights / Power Decay")]
        [Min(0.01f)] public float curveSpeed = 1f;
        [Min(0f)] public float multiplierScale = 1f;

        [Header("Enemies")]
        [Min(0.1f)] public float enemyNavSpeedMultiplier = 1f;
        [Min(0.1f)] public float enemyAnimSpeedMultiplier = 1f;

        [Header("Repair")]
        [Min(0f)] public float repairDurationSeconds = 0f;

        [Header("Monster Damage")]
        [Tooltip("Damage per second.")]
        [Min(0.01f)] public float monsterDamagePerSecond = 1f;
    }

    [Header("Difficulty Settings")]
    public DifficultyConfig low = new DifficultyConfig
    {
        description = "Low.\n\nLights last longer.\nEnemies are slower.\nRepair is instant.\nDamage is lower.",
        curveSpeed = 0.75f,
        multiplierScale = 0.85f,
        enemyNavSpeedMultiplier = 0.85f,
        enemyAnimSpeedMultiplier = 0.9f,
        repairDurationSeconds = 0f,
        monsterDamagePerSecond = 0.5f
    };

    public DifficultyConfig medium = new DifficultyConfig
    {
        description = "Medium.\n\nBalanced settings.",
        curveSpeed = 1f,
        multiplierScale = 1f,
        enemyNavSpeedMultiplier = 1f,
        enemyAnimSpeedMultiplier = 1f,
        repairDurationSeconds = 0f,
        monsterDamagePerSecond = 1f
    };

    public DifficultyConfig high = new DifficultyConfig
    {
        description = "High.\n\nLights go out quicker.\nEnemies are faster.\nRepair takes longer.\nDamage is higher.",
        curveSpeed = 1.35f,
        multiplierScale = 1.2f,
        enemyNavSpeedMultiplier = 1.2f,
        enemyAnimSpeedMultiplier = 1.15f,
        repairDurationSeconds = 0.75f,
        monsterDamagePerSecond = 2f
    };

    enum SelectedDifficulty { Low, Medium, High }
    SelectedDifficulty selectedDifficulty = SelectedDifficulty.Medium;

    bool gameWon;
    bool gameLost;
    bool isPaused;
    bool gameStarted;

    enum CreditsReturnTarget { Start, Pause, Win, Lose }
    CreditsReturnTarget creditsReturnTo = CreditsReturnTarget.Start;

    readonly List<NavMeshAgent> cachedAgents = new List<NavMeshAgent>();
    readonly Dictionary<int, float> baseAgentSpeed = new Dictionary<int, float>();

    Coroutine applyDifficultyRoutine;

    void Awake()
    {
        Time.timeScale = 1f;

        if (roads == null)
            roads = FindFirstObjectByType<RoadPathGenerator>();

        if (powerManager == null)
            powerManager = PowerDecayManager.Instance;

        WireButtons();
        CacheAgentsOnce();

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        SetupDifficultyUI();

        ShowStartScreen();
    }

    void OnDestroy()
    {
        UnhookPlayerDeath();
    }

    void WireButtons()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartGameClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        if (startCreditsButton != null) startCreditsButton.onClick.AddListener(OpenCreditsFromStart);

        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
        if (pauseExitButton != null) pauseExitButton.onClick.AddListener(ReturnToMenu);
        if (pauseCreditsButton != null) pauseCreditsButton.onClick.AddListener(OpenCreditsFromPause);

        if (winExitToMenuButton != null) winExitToMenuButton.onClick.AddListener(ReturnToMenu);
        if (winCreditsButton != null) winCreditsButton.onClick.AddListener(OpenCreditsFromWin);

        if (loseExitToMenuButton != null) loseExitToMenuButton.onClick.AddListener(ReturnToMenu);
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
        if (loseCreditsButton != null) loseCreditsButton.onClick.AddListener(OpenCreditsFromLose);

        if (creditsBackButton != null) creditsBackButton.onClick.AddListener(CloseCredits);
    }

    void SetupDifficultyUI()
    {
        selectedDifficulty = SelectedDifficulty.Medium;

        if (lowToggle != null)
        {
            if (difficultyToggleGroup != null) lowToggle.group = difficultyToggleGroup;
            lowToggle.onValueChanged.AddListener(isOn => { if (isOn) SelectDifficulty(SelectedDifficulty.Low); });
        }

        if (mediumToggle != null)
        {
            if (difficultyToggleGroup != null) mediumToggle.group = difficultyToggleGroup;
            mediumToggle.onValueChanged.AddListener(isOn => { if (isOn) SelectDifficulty(SelectedDifficulty.Medium); });
        }

        if (highToggle != null)
        {
            if (difficultyToggleGroup != null) highToggle.group = difficultyToggleGroup;
            highToggle.onValueChanged.AddListener(isOn => { if (isOn) SelectDifficulty(SelectedDifficulty.High); });
        }

        if (mediumToggle != null)
            mediumToggle.isOn = true;

        UpdateDifficultyDescription();
        EnforceSingleDifficultyToggle();
    }

    void EnforceSingleDifficultyToggle()
    {
        if (lowToggle == null || mediumToggle == null || highToggle == null) return;

        int onCount = (lowToggle.isOn ? 1 : 0) + (mediumToggle.isOn ? 1 : 0) + (highToggle.isOn ? 1 : 0);
        if (onCount == 1) return;

        lowToggle.isOn = false;
        highToggle.isOn = false;
        mediumToggle.isOn = true;
        selectedDifficulty = SelectedDifficulty.Medium;
        UpdateDifficultyDescription();
    }

    void SelectDifficulty(SelectedDifficulty d)
    {
        selectedDifficulty = d;

        if (difficultyToggleGroup == null)
        {
            if (lowToggle != null) lowToggle.SetIsOnWithoutNotify(d == SelectedDifficulty.Low);
            if (mediumToggle != null) mediumToggle.SetIsOnWithoutNotify(d == SelectedDifficulty.Medium);
            if (highToggle != null) highToggle.SetIsOnWithoutNotify(d == SelectedDifficulty.High);
        }

        UpdateDifficultyDescription();
    }

    void UpdateDifficultyDescription()
    {
        if (difficultyDescriptionText == null) return;

        var cfg = GetSelectedDifficultyConfig();
        difficultyDescriptionText.text = (cfg != null && !string.IsNullOrWhiteSpace(cfg.description)) ? cfg.description : "";
    }

    DifficultyConfig GetSelectedDifficultyConfig()
    {
        switch (selectedDifficulty)
        {
            case SelectedDifficulty.Low: return low;
            case SelectedDifficulty.High: return high;
            default: return medium;
        }
    }

    PowerDecayManager.Difficulty GetSelectedPowerDifficulty()
    {
        switch (selectedDifficulty)
        {
            case SelectedDifficulty.Low: return PowerDecayManager.Difficulty.Easy;
            case SelectedDifficulty.High: return PowerDecayManager.Difficulty.Hard;
            default: return PowerDecayManager.Difficulty.Normal;
        }
    }

    void CacheAgentsOnce()
    {
        cachedAgents.Clear();
        cachedAgents.AddRange(FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None));
    }

    void UnhookPlayerDeath()
    {
        if (playerHealth != null)
            playerHealth.OnPlayerDied -= LoseGame;
    }

    public void RegisterPlayer(PlayerHealth ph)
    {
        UnhookPlayerDeath();
        playerHealth = ph;

        if (playerHealth != null)
            playerHealth.OnPlayerDied += LoseGame;
    }

    float GetSessionSecondsFromDropdown()
    {
        if (sessionDurationDropdown == null) return Mathf.Max(30f, fallbackSessionSeconds);
        if (sessionDurationDropdown.options == null || sessionDurationDropdown.options.Count == 0) return Mathf.Max(30f, fallbackSessionSeconds);

        int idx = Mathf.Clamp(sessionDurationDropdown.value, 0, sessionDurationDropdown.options.Count - 1);
        string label = sessionDurationDropdown.options[idx].text ?? "";
        label = label.Trim().ToLowerInvariant();

        // Minutes like "10 min", "10m"
        var minMatch = Regex.Match(label, @"(\d+(\.\d+)?)\s*(min|m)\b");
        if (minMatch.Success)
        {
            float mins = ParseFloat(minMatch.Groups[1].Value, 10f);
            return Mathf.Max(30f, mins * 60f);
        }

        // Multipliers like "x1.5", "1.5x"
        var xMatch1 = Regex.Match(label, @"x\s*(\d+(\.\d+)?)");
        var xMatch2 = Regex.Match(label, @"(\d+(\.\d+)?)\s*x");
        if (xMatch1.Success || xMatch2.Success)
        {
            string v = xMatch1.Success ? xMatch1.Groups[1].Value : xMatch2.Groups[1].Value;
            float mul = ParseFloat(v, 1f);
            return Mathf.Max(30f, baseSessionSecondsForMultiplier * mul);
        }

        // Plain number means minutes
        var numOnly = Regex.Match(label, @"(\d+(\.\d+)?)");
        if (numOnly.Success && label.Length <= 6)
        {
            float mins = ParseFloat(numOnly.Groups[1].Value, 10f);
            return Mathf.Max(30f, mins * 60f);
        }

        return Mathf.Max(30f, fallbackSessionSeconds);
    }

    static float ParseFloat(string s, float fallback)
    {
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
        if (float.TryParse(s, out v)) return v;
        return fallback;
    }

    IEnumerator ApplyDifficultyAfterSpawn()
    {
        yield return null;
        yield return null;
        yield return null;

        for (int i = 0; i < 30; i++)
        {
            ApplyDifficultyToWorldOnce();

            var dmg = FindAllIncludingInactive<MonsterDamage>();
            if (dmg.Count > 0) break;

            yield return null;
        }

        applyDifficultyRoutine = null;
    }

    static List<T> FindAllIncludingInactive<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return new List<T>(found);
#else
        var found = Object.FindObjectsOfType<T>(true);
        return new List<T>(found);
#endif
    }

    void ApplyDifficultyToWorldOnce()
    {
        var cfg = GetSelectedDifficultyConfig();
        float sessionSeconds = GetSessionSecondsFromDropdown();

        if (logDifficultyApply)
            Debug.Log($"[StartSettings] Difficulty={selectedDifficulty} Session={sessionSeconds}s DPS={cfg.monsterDamagePerSecond} Repair={cfg.repairDurationSeconds}");

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null)
        {
            powerManager.difficulty = GetSelectedPowerDifficulty();
            powerManager.sessionLengthSeconds = sessionSeconds;

            if (powerManager.difficulty == PowerDecayManager.Difficulty.Easy)
            {
                powerManager.easyCurveSpeed = Mathf.Max(0.01f, cfg.curveSpeed);
                powerManager.easyMultiplierScale = Mathf.Max(0f, cfg.multiplierScale);
            }
            else if (powerManager.difficulty == PowerDecayManager.Difficulty.Normal)
            {
                powerManager.normalCurveSpeed = Mathf.Max(0.01f, cfg.curveSpeed);
                powerManager.normalMultiplierScale = Mathf.Max(0f, cfg.multiplierScale);
            }
            else
            {
                powerManager.hardCurveSpeed = Mathf.Max(0.01f, cfg.curveSpeed);
                powerManager.hardMultiplierScale = Mathf.Max(0f, cfg.multiplierScale);
            }
        }

        var repairs = FindAllIncludingInactive<PowerRepairInteraction>();
        for (int i = 0; i < repairs.Count; i++)
        {
            if (repairs[i] == null) continue;
            repairs[i].repairDurationSeconds = Mathf.Max(0f, cfg.repairDurationSeconds);
        }

        var wanders = FindAllIncludingInactive<MonsterWander>();
        for (int i = 0; i < wanders.Count; i++)
        {
            var w = wanders[i];
            if (w == null) continue;

            var agent = w.GetComponent<NavMeshAgent>();
            if (agent == null) continue;

            int id = agent.GetInstanceID();
            if (!baseAgentSpeed.ContainsKey(id))
                baseAgentSpeed[id] = agent.speed;

            agent.speed = Mathf.Max(0.1f, baseAgentSpeed[id] * cfg.enemyNavSpeedMultiplier);
        }

        var syncs = FindAllIncludingInactive<MonsterWalkSpeedSync>();
        for (int i = 0; i < syncs.Count; i++)
        {
            var s = syncs[i];
            if (s == null) continue;
            s.speedMultiplier = Mathf.Max(0.1f, cfg.enemyAnimSpeedMultiplier);
        }

        var damages = FindAllIncludingInactive<MonsterDamage>();
        for (int i = 0; i < damages.Count; i++)
        {
            var d = damages[i];
            if (d == null) continue;

            float dps = Mathf.Max(0.01f, cfg.monsterDamagePerSecond);
            d.damagePerTick = dps;
            d.damageInterval = 1f;
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (creditsPanel != null && creditsPanel.activeSelf)
            {
                CloseCredits();
                return;
            }

            if (!gameStarted) return;
            if (gameWon || gameLost) return;

            if (isPaused) ResumeGame();
            else PauseGame();
        }

        if (!gameStarted) return;
        if (gameWon || gameLost) return;
        if (creditsPanel != null && creditsPanel.activeSelf) return;

        if (powerManager == null) powerManager = PowerDecayManager.Instance;

        if (powerManager != null)
        {
            float session = powerManager.EffectiveSessionLengthSeconds;
            if (powerManager.ElapsedSeconds >= session)
                WinGame();
        }
    }

    void ApplyPauseState(bool paused)
    {
        isPaused = paused;

        Time.timeScale = paused ? 0f : 1f;

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null) powerManager.isRunning = !paused;

        if (pauseAudio)
            AudioListener.pause = paused;

        if (pauseNavMeshAgents)
        {
            for (int i = 0; i < cachedAgents.Count; i++)
            {
                var a = cachedAgents[i];
                if (!a) continue;

                a.isStopped = paused;
                a.updatePosition = !paused;
                a.updateRotation = !paused;
            }
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void HideAllNonCreditsPanels()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
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

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void ShowStartScreen()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        gameStarted = false;
        gameWon = false;
        gameLost = false;

        ApplyPauseState(true);

        SelectDifficulty(SelectedDifficulty.Medium);
        if (mediumToggle != null) mediumToggle.isOn = true;

        if (seedInput != null)
        {
            seedInput.interactable = true;
            seedInput.ActivateInputField();
        }
    }

    void ShowGameplayHUD()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        gameStarted = true;
        gameWon = false;
        gameLost = false;

        ApplyPauseState(false);

        if (seedInput != null)
            seedInput.DeactivateInputField();
    }

    void OnStartGameClicked()
    {
        if (roads == null)
        {
            Debug.LogError("RoadPathGenerator reference is missing on GameUIController.");
            return;
        }

        gameWon = false;
        gameLost = false;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

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

        UnhookPlayerDeath();
        playerHealth = null;

        roads.GenerateAndSpawn();
        CacheAgentsOnce();

        if (powerManager == null) powerManager = PowerDecayManager.Instance;
        if (powerManager != null) powerManager.ResetTimer(true);

        ShowGameplayHUD();

        if (applyDifficultyRoutine != null) StopCoroutine(applyDifficultyRoutine);
        applyDifficultyRoutine = StartCoroutine(ApplyDifficultyAfterSpawn());
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
        if (gameWon || gameLost) return;

        if (pausePanel != null) pausePanel.SetActive(true);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        ApplyPauseState(true);
    }

    public void ResumeGame()
    {
        if (gameWon || gameLost) return;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (gamePlayPanel != null) gamePlayPanel.SetActive(true);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        ApplyPauseState(false);
    }

    public void WinGame()
    {
        if (gameWon || gameLost) return;

        gameWon = true;

        HideAllNonCreditsPanels();
        ForceShowPanel(winPanel);

        if (creditsPanel != null) creditsPanel.SetActive(false);
        ApplyPauseState(true);
    }

    public void LoseGame()
    {
        if (gameWon || gameLost) return;

        gameLost = true;

        HideAllNonCreditsPanels();
        ForceShowPanel(losePanel);

        if (creditsPanel != null) creditsPanel.SetActive(false);
        ApplyPauseState(true);
    }

    void ReturnToMenu()
    {
        ShowStartScreen();
    }

    void RestartGame()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        OnStartGameClicked();
    }

    void OpenCreditsFromStart() { creditsReturnTo = CreditsReturnTarget.Start; OpenCredits(); }
    void OpenCreditsFromPause() { creditsReturnTo = CreditsReturnTarget.Pause; OpenCredits(); }
    void OpenCreditsFromWin() { creditsReturnTo = CreditsReturnTarget.Win; OpenCredits(); }
    void OpenCreditsFromLose() { creditsReturnTo = CreditsReturnTarget.Lose; OpenCredits(); }

    void OpenCredits()
    {
        if (creditsPanel == null) return;

        ApplyPauseState(true);
        HideAllNonCreditsPanels();
        ForceShowPanel(creditsPanel);
    }

    void CloseCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);

        switch (creditsReturnTo)
        {
            case CreditsReturnTarget.Start:
                ShowStartScreen();
                break;

            case CreditsReturnTarget.Pause:
                if (pausePanel != null) pausePanel.SetActive(true);
                if (gamePlayPanel != null) gamePlayPanel.SetActive(false);
                ApplyPauseState(true);
                break;

            case CreditsReturnTarget.Win:
                HideAllNonCreditsPanels();
                ForceShowPanel(winPanel);
                ApplyPauseState(true);
                break;

            case CreditsReturnTarget.Lose:
                HideAllNonCreditsPanels();
                ForceShowPanel(losePanel);
                ApplyPauseState(true);
                break;
        }
    }

    public void HotkeyRestartRun()
    {
        // Only restart if a run is active
        if (!gameStarted) return;

        // Make sure we are not paused
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Same behaviour as clicking Restart on lose panel:
        RestartGame();
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
