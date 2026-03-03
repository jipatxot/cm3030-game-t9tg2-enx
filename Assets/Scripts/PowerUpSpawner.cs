using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PowerUpSpawnRule
{
    [Header("Identity")]
    public string id = "HealthMaxBoost";

    [Header("Prefab")]
    public GameObject prefab;

    [Header("Limits")]
    public int maxActiveOnMap = 3;
    public int maxTotalSpawns = 10;

    [Header("Effect")]
    [Range(0f, 1f)] public float durationPercentOfRound = 0.10f;
    public float maxHealthBonus = 10f;

    [Header("Placement")]
    public float yOffset = 0.2f;
}

public class PowerUpSpawner : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform powerUpRoot;

    [Header("Ground snapping (match StreetFurnitureGenerator style)")]
    public Collider groundCollider;
    public LayerMask groundMask = ~0;
    public float snapStartHeight = 200f;

    [Header("Rules")]
    public List<PowerUpSpawnRule> rules = new List<PowerUpSpawnRule>();

    System.Random rng;

    readonly Dictionary<string, int> activeCount = new Dictionary<string, int>();
    readonly Dictionary<string, int> totalSpawned = new Dictionary<string, int>();

    void OnEnable()
    {
        PowerUpPickup.OnPickedUp += HandlePickedUp;

        if (roads != null) roads.OnGenerated += OnRoadsGenerated;
    }

    void OnDisable()
    {
        PowerUpPickup.OnPickedUp -= HandlePickedUp;

        if (roads != null) roads.OnGenerated -= OnRoadsGenerated;
    }

    void Awake()
    {
        if (powerUpRoot == null) powerUpRoot = transform;
    }

    void OnRoadsGenerated()
    {
        int baseSeed = roads != null ? Mathf.Max(1, roads.seed) : 1;
        rng = new System.Random(unchecked(baseSeed + 9001));

        activeCount.Clear();
        totalSpawned.Clear();

        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r == null) continue;

            activeCount[r.id] = 0;
            totalSpawned[r.id] = 0;

            FillToActiveCap(r);
        }
    }

    void HandlePickedUp(string id)
    {
        if (!activeCount.ContainsKey(id)) return;
        activeCount[id] = Mathf.Max(0, activeCount[id] - 1);

        var rule = rules.Find(r => r != null && r.id == id);
        if (rule == null) return;

        FillToActiveCap(rule);
    }

    void FillToActiveCap(PowerUpSpawnRule r)
    {
        if (roads == null || roads.Map == null) return;
        if (r.prefab == null) return;

        if (!activeCount.ContainsKey(r.id)) activeCount[r.id] = 0;
        if (!totalSpawned.ContainsKey(r.id)) totalSpawned[r.id] = 0;

        int safety = 200;

        while (activeCount[r.id] < Mathf.Max(0, r.maxActiveOnMap) &&
               totalSpawned[r.id] < Mathf.Max(0, r.maxTotalSpawns) &&
               safety-- > 0)
        {
            if (!TryPickRandomRoadCell(out int x, out int y)) break;

            Vector3 candidate = roads.GridToWorld(x, y);

            if (!TrySnapToGroundStrict(candidate, r.yOffset, out var pos))
                continue;

            var go = Instantiate(r.prefab, pos, Quaternion.identity, powerUpRoot);

            var pickup = go.GetComponent<PowerUpPickup>();
            if (pickup != null)
            {
                pickup.powerUpId = r.id;
                pickup.type = PowerUpType.MaxHealthBoost;
                pickup.value = r.maxHealthBonus;
                pickup.durationMode = PowerUpDurationMode.PercentOfGameTime;
                pickup.duration = Mathf.Clamp01(r.durationPercentOfRound);
            }

            activeCount[r.id] += 1;
            totalSpawned[r.id] += 1;
        }
    }

    bool TryPickRandomRoadCell(out int rx, out int ry)
    {
        rx = 0;
        ry = 0;

        int w = roads.Width;
        int h = roads.Height;

        for (int tries = 0; tries < 4000; tries++)
        {
            int x = rng.Next(1, w - 1);
            int y = rng.Next(1, h - 1);

            if (roads.Map[x, y] != RoadPathGenerator.CellType.Road) continue;

            rx = x;
            ry = y;
            return true;
        }

        return false;
    }

    bool TrySnapToGroundStrict(Vector3 pos, float extraY, out Vector3 snapped)
    {
        float maxDist = snapStartHeight * 2f;
        Vector3 start = new Vector3(pos.x, pos.y + snapStartHeight, pos.z);
        Ray ray = new Ray(start, Vector3.down);

        if (groundCollider != null)
        {
            if (!groundCollider.Raycast(ray, out var hit, maxDist))
            {
                snapped = default;
                return false;
            }

            snapped = new Vector3(pos.x, hit.point.y + extraY, pos.z);
            return true;
        }

        if (Physics.Raycast(start, Vector3.down, out var hit2, maxDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            snapped = new Vector3(pos.x, hit2.point.y + extraY, pos.z);
            return true;
        }

        snapped = default;
        return false;
    }
}
