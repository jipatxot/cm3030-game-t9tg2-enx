using UnityEngine;

public class MonsterDamage : MonoBehaviour
{
    public int damagePerHit = 1;
    public float hitCooldown = 0.6f;

    float nextHitTime;

    void OnTriggerStay(Collider other)
    {
        TryDamage(other);
    }

    void OnCollisionStay(Collision collision)
    {
        TryDamage(collision.collider);
    }

    void TryDamage(Collider other)
    {
        if (other == null) return;
        if (!other.CompareTag("Player")) return;

        if (Time.time < nextHitTime) return;

        if (LightSource.IsPositionInAnySafeZone(other.transform.position)) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health == null) return;

        health.ApplyDamage(damagePerHit);
        nextHitTime = Time.time + hitCooldown;
    }
}
