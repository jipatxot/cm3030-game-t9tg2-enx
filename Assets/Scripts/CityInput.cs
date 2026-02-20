using UnityEngine;
using UnityEngine.InputSystem;

public class CityInput : MonoBehaviour
{
    public GameUIController ui;          // drag in inspector (optional)
    public RoadPathGenerator roads;      // keep if you want, optional

    private PlayerControls controls;

    void Awake()
    {
        controls = new PlayerControls();

        if (ui == null)
            ui = FindFirstObjectByType<GameUIController>();

        if (roads == null)
            roads = FindFirstObjectByType<RoadPathGenerator>();
    }

    void OnEnable()
    {
        if (controls == null) controls = new PlayerControls();

        controls.Enable();
        controls.Gameplay.Regenerate.performed += OnRegenerate;
    }

    void OnDisable()
    {
        if (controls == null) return;

        controls.Gameplay.Regenerate.performed -= OnRegenerate;
        controls.Disable();
    }

    private void OnRegenerate(InputAction.CallbackContext ctx)
    {
        // Preferred: go through UI controller so timer resets, difficulty reapplies, etc.
        if (ui != null)
        {
            ui.HotkeyRestartRun();
            return;
        }

        // Fallback: at least regenerate the map
        if (roads != null)
            roads.GenerateAndSpawn();
        else
            Debug.LogWarning("CityInput: roads reference not set.");
    }
}
