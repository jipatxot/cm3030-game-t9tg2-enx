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
        float agentR = 0f;
        var a = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (a != null) agentR = a.radius;

        float playerR = 0.35f; // matches your PlayerSpawner controller radius
        var cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) playerR = cc.radius;

        // Effective hit range matches what you see in-game
        float effectiveRange = attackRange + agentR + playerR;

        float sqrDistance = toPlayer.sqrMagnitude;
        float sqrRange = effectiveRange * effectiveRange;

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
