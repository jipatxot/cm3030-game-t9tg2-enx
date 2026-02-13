using UnityEngine;
using UnityEngine.InputSystem;

public class CityInput : MonoBehaviour
{
    public RoadPathGenerator roads;

    private PlayerControls controls;

    void Awake()
    {
        controls = new PlayerControls();

        if (roads == null)
            roads = FindFirstObjectByType<RoadPathGenerator>();
    }

    void OnEnable()
    {
        controls.Enable();
        controls.Gameplay.Regenerate.performed += OnRegenerate;
    }

    void OnDisable()
    {
        controls.Gameplay.Regenerate.performed -= OnRegenerate;
        controls.Disable();
    }

    private void OnRegenerate(InputAction.CallbackContext ctx)
    {
        if (roads != null)
            roads.GenerateAndSpawn();
        else
            UnityEngine.Debug.LogWarning("CityInput: roads reference not set."); 
    }
}
