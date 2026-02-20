using UnityEngine;

public class PlayerRootLightHealer : MonoBehaviour
{
    [Header("Find Player")]
    public string playerTag = "Player";
    public float rebindInterval = 0.25f;

    [Header("Heal near lamps")]
    public float healRadius = 2.5f;

    [Tooltip("Heal only if current/baseMax is >= this threshold.")]
    [Range(0f, 1f)]
    public float minHealth01ToHeal = 0.25f;

    [Range(0f, 1f)]
    public float minPower01ForHeal = 0.25f;

    [Header("Stepped healing (once per second)")]
    [Tooltip("How much each tick heals. Example: 0.5 = +0.5 HP per second.")]
    public float healStepAmount = 0.5f;

    [Tooltip("Seconds between heal ticks. Leave at 1 for '+0.5 every second' behavior.")]
    public float tickSeconds = 1f;

    Transform player;
    PlayerHealth playerHealth;
    float nextRebindTime;

    float healAccumulatorSeconds;

    void Update()
    {
        RebindIfNeeded();
        if (player == null || playerHealth == null) return;
        if (playerHealth.currentHealth <= 0f) return;

        // While boosted, only a health pack can heal above base.
        // Lights do nothing during boost.
        if (playerHealth.IsBoostedMaxActive())
        {
            healAccumulatorSeconds = 0f;
            return;
        }

        float baseMax = playerHealth.GetBaseMaxHealth();
        if (baseMax <= 0.01f) return;

        // Gate healing under threshold (based on base max)
        float health01 = playerHealth.currentHealth / baseMax;
        if (health01 < minHealth01ToHeal)
        {
            healAccumulatorSeconds = 0f;
            return;
        }

        // Must be near a strong lamp
        if (!SafeZoneRegistry.HasStrongLampNearby(player.position, healRadius, minPower01ForHeal))
        {
            healAccumulatorSeconds = 0f;
            return;
        }

        // Already at (or above) base max
        if (playerHealth.currentHealth >= baseMax - 0.0001f)
        {
            healAccumulatorSeconds = 0f;
            return;
        }

        float step = Mathf.Max(0.0001f, healStepAmount);
        float secondsPerTick = Mathf.Max(0.05f, tickSeconds);

        healAccumulatorSeconds += Time.deltaTime;

        while (healAccumulatorSeconds >= secondsPerTick)
        {
            healAccumulatorSeconds -= secondsPerTick;

            // Heal in clean steps, capped to base max
            playerHealth.RestoreHealthCapped(step, baseMax);

            if (playerHealth.currentHealth >= baseMax - 0.0001f)
            {
                healAccumulatorSeconds = 0f;
                break;
            }
        }
    }

    void RebindIfNeeded()
    {
        if (player != null && playerHealth != null) return;
        if (Time.unscaledTime < nextRebindTime) return;

        nextRebindTime = Time.unscaledTime + Mathf.Max(0.05f, rebindInterval);

        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null) return;

        player = go.transform;

        playerHealth = go.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = go.GetComponentInChildren<PlayerHealth>(true);
    }
}
