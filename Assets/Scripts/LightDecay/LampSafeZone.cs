using UnityEngine;

[RequireComponent(typeof(StreetLampPower))]
public class LampSafeZone : MonoBehaviour, ISafeZone
{
    [Header("Safe Zone")]
    public float safeRadius = 3f;
    [Range(0f, 1f)] public float minPower01ForSafe = 0.05f;

    [Header("Optional Healing")]
    public float restoreRadius = 2.5f;
    public int healthRestoreAmount = 1;
    public float healCooldownSeconds = 2f;

    StreetLampPower lampPower;
    Transform playerTransform;
    float nextHealTime;
    bool wasPlayerInRestoreRange;

    void Awake()
    {
        lampPower = GetComponent<StreetLampPower>();
    }

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
        if (lampPower == null || lampPower.NormalizedPower01 < minPower01ForSafe) return;
        if (Time.time < nextHealTime) return;

        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform == null) return;

        bool inRestoreRange = Vector3.Distance(playerTransform.position, transform.position) <= restoreRadius;

        if (!inRestoreRange)
        {
            wasPlayerInRestoreRange = false;
            return;
        }

        if (wasPlayerInRestoreRange) return;

        var health = playerTransform.GetComponent<PlayerHealth>();
        if (health == null) return;

        health.RestoreHealth(healthRestoreAmount);
        nextHealTime = Time.time + healCooldownSeconds;
        wasPlayerInRestoreRange = true;
    }

    public bool IsPositionSafe(Vector3 position)
    {
        if (lampPower == null || lampPower.NormalizedPower01 < minPower01ForSafe) return false;
        return Vector3.Distance(position, transform.position) <= safeRadius;
    }
}
