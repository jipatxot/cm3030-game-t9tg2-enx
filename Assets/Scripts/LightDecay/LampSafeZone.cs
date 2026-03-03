using UnityEngine;

[RequireComponent(typeof(LightPowerDecay))]
public class LampSafeZone : MonoBehaviour, ISafeZone
{
    [Header("Safe Zone")]
    public float safeRadius = 3f;
    [Range(0f, 1f)] public float minPower01ForSafe = 0.05f;

    LightPowerDecay lampPower;

    public float Power01
    {
        get { return lampPower != null ? lampPower.NormalizedPower01 : 0f; }
    }

    void Awake()
    {
        lampPower = GetComponent<LightPowerDecay>();
    }

    void OnEnable()
    {
        SafeZoneRegistry.Register(this);
    }

    void OnDisable()
    {
        SafeZoneRegistry.Unregister(this);
    }

    public bool IsPositionSafe(Vector3 position)
    {
        if (lampPower == null || lampPower.NormalizedPower01 < minPower01ForSafe) return false;
        return Vector3.Distance(position, transform.position) <= safeRadius;
    }
}
