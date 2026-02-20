using UnityEngine;
using UnityEngine.SceneManagement;


/// Attach this to your win panel (or any object in that scene).
/// In the Button's OnClick, call one of the public methods below.
/// It will:
/// 1) Stop & reset the sunrise effect (remove overlay and restore lighting)
/// 2) (Optional) reset LightDecay state for an in-scene restart OR reload a scene
public class SunriseAndLightCleanup : MonoBehaviour
{
    [Header("References")]
    public SunriseFinaleEffect sunriseEffect;
    public PowerDecayManager manager;

    [Header("Optional: In-scene Restart")]
    [Tooltip("If set, this object will be hidden on restart (e.g., WinPanel root).")]
    public GameObject winPanelToHide;

    void Awake()
    {
        if (sunriseEffect == null)
            sunriseEffect = FindFirstObjectByType<SunriseFinaleEffect>(FindObjectsInactive.Include);

        if (manager == null)
            manager = PowerDecayManager.Instance;
    }


    /// Call this from your button BEFORE you load another scene.
    /// It removes the sunrise overlay and restores ambient/light to the original values.
    public void CleanupOnly()
    {
        if (sunriseEffect != null)
            sunriseEffect.StopAndReset(true);
    }


    /// "Restart run" without reloading the scene:
    /// - Stop sunrise
    /// - Reset timer
    /// - Restore all lights (will respect hard cap if your LightPowerDecay enforces it)
    /// - Hide win panel if assigned
    public void RestartInSameScene()
    {
        CleanupOnly();

        if (manager != null)
            manager.ResetTimer(true);

#if UNITY_2023_1_OR_NEWER
        var lights = Object.FindObjectsByType<LightPowerDecay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var lights = Object.FindObjectsOfType<LightPowerDecay>(true);
#endif
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].RestoreToFull();
        }

        if (winPanelToHide != null)
            winPanelToHide.SetActive(false);
    }

    /// Reload current scene after cleanup (simple reset).
    public void ReloadCurrentScene()
    {
        CleanupOnly();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// Load a named scene after cleanup (e.g., Main Menu).
    public void LoadSceneByName(string sceneName)
    {
        CleanupOnly();
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}
