using System.Collections.Generic;
using UnityEngine;

public interface ISafeZone
{
    bool IsPositionSafe(Vector3 position);
}

public static class SafeZoneRegistry
{
    static readonly List<ISafeZone> Zones = new List<ISafeZone>();

    public static void Register(ISafeZone zone)
    {
        if (zone == null || Zones.Contains(zone)) return;
        Zones.Add(zone);
    }

    public static void Unregister(ISafeZone zone)
    {
        if (zone == null) return;
        Zones.Remove(zone);
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
}
