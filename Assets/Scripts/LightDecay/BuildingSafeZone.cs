using UnityEngine;

public class BuildingSafeZone : MonoBehaviour, ISafeZone
{
    [Header("Safe Zone")]
    public float safeRadius = 4f;

    [Header("Optional Healing")]
    public float restoreRadius = 2.5f;
    public int healthRestoreAmount = 2;
    public float healCooldownSeconds = 2f;

    Transform playerTransform;
    float nextHealTime;

    void OnEnable()
    {
        SafeZoneRegistry.Register(this);
    }

    void OnDisable()
    {
        SafeZoneRegistry.Unregister(this);
    }

    void Update()
    {
        if (healthRestoreAmount <= 0) return;
        if (Time.time < nextHealTime) return;

        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform == null) return;

        if (Vector3.Distance(playerTransform.position, transform.position) <= restoreRadius)
        {
            var health = playerTransform.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.RestoreHealth(healthRestoreAmount);
                nextHealTime = Time.time + healCooldownSeconds;
            }
        }
    }

    public bool IsPositionSafe(Vector3 position)
    {
        return Vector3.Distance(position, transform.position) <= safeRadius;
    }
}
