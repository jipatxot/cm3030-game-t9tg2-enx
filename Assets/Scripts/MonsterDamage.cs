using UnityEngine;

public class MonsterDamage : MonoBehaviour
{
    public int damagePerTick = 1;
    public float damageInterval = 1f;
    public float attackRange = 1.4f;

    float nextDamageTime;
    Transform playerTransform;
    PlayerHealth playerHealth;

    void Update()
    {
        if (!TryBindPlayer()) return;
        if (playerTransform == null || playerHealth == null) return;

        if (SafeZoneRegistry.IsPositionSafe(playerTransform.position)) return;

        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;
        float sqrDistance = toPlayer.sqrMagnitude;
        float sqrRange = attackRange * attackRange;

        if (sqrDistance > sqrRange) return;
        if (Time.time < nextDamageTime) return;

        playerHealth.ApplyDamage(damagePerTick);
        nextDamageTime = Time.time + Mathf.Max(0.05f, damageInterval);
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
