using UnityEngine;

public enum PowerUpDurationMode
{
    FixedSeconds,
    PercentOfGameTime
}

public class PowerUpPickup : MonoBehaviour
{
    [Header("ID (used by spawner)")]
    public string powerUpId = "HealthMaxBoost";

    [Header("Type")]
    public PowerUpType type = PowerUpType.MaxHealthBoost;

    [Header("Tuning")]
    public float value = 10f;

    [Tooltip("If PercentOfGameTime: 0.10 = 10% of round.")]
    public float duration = 0.10f;

    public PowerUpDurationMode durationMode = PowerUpDurationMode.PercentOfGameTime;

    [Header("Player filter")]
    public string playerTag = "Player";

    public static System.Action<string> OnPickedUp;

    void OnTriggerEnter(Collider other)
    {
        // Only let the player collect it
        // (stops child colliders like HalfHeart triggering the pickup)
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            return;

        var applier = FindApplier(other);
        if (applier == null)
        {
            Debug.Log("NO PowerUpApplier found (parent chain + scene search).");
            return;
        }

        float seconds = durationMode == PowerUpDurationMode.FixedSeconds
            ? Mathf.Max(0f, duration)
            : Mathf.Max(0f, GetRoundDurationSeconds() * Mathf.Clamp01(duration));

        applier.Apply(type, value, seconds);

        OnPickedUp?.Invoke(powerUpId);

        Destroy(gameObject);
    }

    PowerUpApplier FindApplier(Collider other)
    {
        // Fast path: in parent chain of the collider that entered
        var a = other.GetComponentInParent<PowerUpApplier>();
        if (a != null) return a;

        // Common case: collider is on "Player" child object but applier is on PlayerRoot
        // Try from the tagged Player root object
        var playerGo = GameObject.FindGameObjectWithTag(playerTag);
        if (playerGo != null)
        {
            a = playerGo.GetComponentInParent<PowerUpApplier>();
            if (a != null) return a;

            a = playerGo.GetComponentInChildren<PowerUpApplier>(true);
            if (a != null) return a;
        }

#if UNITY_2023_1_OR_NEWER
        // Final fallback: find any applier in scene
        a = FindFirstObjectByType<PowerUpApplier>(FindObjectsInactive.Exclude);
#else
        a = FindObjectOfType<PowerUpApplier>();
#endif
        return a;
    }

    float GetRoundDurationSeconds()
    {
        if (PowerDecayManager.Instance != null)
            return Mathf.Max(1f, PowerDecayManager.Instance.GetSessionDurationSeconds());

        return 60f;
    }
}
