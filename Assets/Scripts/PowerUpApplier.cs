using System.Collections;
using UnityEngine;

public class PowerUpApplier : MonoBehaviour
{
    [Header("Refs (optional)")]
    public PlayerHealth playerHealth;

    Coroutine maxHealthBoostCo;

    void Awake()
    {
        TryBind();
    }

    void TryBind()
    {
        if (playerHealth != null) return;

        // PlayerHealth is typically on the spawned Player under PlayerRoot
        playerHealth = GetComponentInChildren<PlayerHealth>(true);
        if (playerHealth != null) return;

        // Fallback: find by type in scene (safe if only 1 player)
#if UNITY_2023_1_OR_NEWER
        playerHealth = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
#else
        playerHealth = FindObjectOfType<PlayerHealth>();
#endif
    }

    public void Apply(PowerUpType type, float value, float durationSeconds)
    {
        if (type != PowerUpType.MaxHealthBoost) return;

        // Player might have spawned after Awake, so bind again here
        TryBind();

        Debug.Log("Bound PlayerHealth on: " + playerHealth.gameObject.name);

        if (playerHealth == null)
        {
            Debug.LogWarning("PowerUpApplier: playerHealth is still NULL. It must exist on the spawned Player.");
            return;
        }

        Restart(ref maxHealthBoostCo, () => MaxHealthBoostRoutine(value, durationSeconds));
    }

    void Restart(ref Coroutine slot, System.Func<IEnumerator> routineFactory)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(routineFactory());
    }

    IEnumerator MaxHealthBoostRoutine(float bonusMax, float seconds)
    {
        float baseMax = playerHealth.GetBaseMaxHealth();
        float boostedMax = baseMax + Mathf.Max(0f, bonusMax);

        // Heal to boosted max and raise cap
        playerHealth.SetMaxHealthTemporary(boostedMax, healToNewMax: true);

        yield return new WaitForSeconds(Mathf.Max(0f, seconds));

        // Reset cap and clamp HP down if needed
        playerHealth.ResetMaxHealthToBaseAndClamp();

        maxHealthBoostCo = null;
    }
}
