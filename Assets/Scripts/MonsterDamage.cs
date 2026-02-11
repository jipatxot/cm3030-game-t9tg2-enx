using UnityEngine;

public class MonsterDamage : MonoBehaviour
{
    public int damagePerTick = 1;
    public float damageInterval = 1f;
    public float attackRange = 1.4f;

    float nextDamageTime;
    bool isPlayerInRange;
    Transform playerTransform;
    PlayerHealth playerHealth;

    void Update()
    {
        if (!TryBindPlayer())
        {
            isPlayerInRange = false;
            return;
        }

        if (playerTransform == null || playerHealth == null)
        {
            isPlayerInRange = false;
            return;
        }

        if (SafeZoneRegistry.IsPositionSafe(playerTransform.position))
        {
            isPlayerInRange = false;
            return;
        }

        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;
        float sqrDistance = toPlayer.sqrMagnitude;
        float sqrRange = attackRange * attackRange;

        bool enteredRange = sqrDistance <= sqrRange;
        if (!enteredRange)
        {
            isPlayerInRange = false;
            return;
        }

        float interval = Mathf.Max(0.05f, damageInterval);

        if (!isPlayerInRange)
        {
            isPlayerInRange = true;
            nextDamageTime = Time.time + interval;
            return;
        }

        if (Time.time < nextDamageTime) return;

        playerHealth.ApplyDamage(damagePerTick);
        nextDamageTime = Time.time + interval;
    }

    bool TryBindPlayer()
    {
        if (playerTransform != null && playerHealth != null)
            return true;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;

        playerTransform = player.transform;
        playerHealth = player.GetComponent<PlayerHealth>();

        return playerHealth != null;
    }
}
