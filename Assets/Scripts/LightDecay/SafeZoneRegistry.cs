using System.Collections.Generic;
using UnityEngine;

public interface ISafeZone
{
    bool IsPositionSafe(Vector3 position);
}

public static class SafeZoneRegistry
{
    static readonly List<ISafeZone> Zones = new List<ISafeZone>();
    static readonly List<LampSafeZone> Lamps = new List<LampSafeZone>();

    public static void Register(ISafeZone zone)
    {
        if (zone == null) return;

        if (!Zones.Contains(zone))
            Zones.Add(zone);

        var lamp = zone as LampSafeZone;
        if (lamp != null && !Lamps.Contains(lamp))
            Lamps.Add(lamp);
    }

    public static void Unregister(ISafeZone zone)
    {
        if (zone == null) return;

        Zones.Remove(zone);

        var lamp = zone as LampSafeZone;
        if (lamp != null)
            Lamps.Remove(lamp);
    }

    public static bool IsPositionSafe(Vector3 position)
    {
        for (int i = 0; i < Zones.Count; i++)
        {
            var zone = Zones[i];
            if (zone == null) continue;
            if (zone.IsPositionSafe(position)) return true;
        }

        return false;
    }

    public static bool HasStrongLampNearby(Vector3 position, float radius, float minPower01)
    {
        float r2 = radius * radius;
        float min = Mathf.Clamp01(minPower01);

        for (int i = 0; i < Lamps.Count; i++)
        {
            var l = Lamps[i];
            if (l == null) continue;
            if (l.Power01 < min) continue;

            Vector3 d = l.transform.position - position;
            d.y = 0f;

            if (d.sqrMagnitude <= r2)
                return true;
        }

        return false;
    }
}
