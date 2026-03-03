using UnityEngine;

public class BuildingSafeZone : MonoBehaviour, ISafeZone
{
    [Header("Safe Zone")]
    public float safeRadius = 4f;

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
        return Vector3.Distance(position, transform.position) <= safeRadius;
    }
}
