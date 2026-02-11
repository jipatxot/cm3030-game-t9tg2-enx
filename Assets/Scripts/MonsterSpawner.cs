using UnityEngine;
using UnityEngine.AI;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public GameObject monsterPrefab;
    public Transform monstersRoot;

    [Header("Count")]
    public int monsterCount = 8;

    [Header("Monster Size")]
    [Tooltip("1 = original prefab size. 0.25 = quarter size, etc.")]
    public float monsterScale = 0.25f;

    [Header("NavMeshAgent scaling")]
    [Tooltip("If true, scales agent radius/height with the monsterScale.")]
    public bool scaleNavMeshAgent = true;

    [Header("Spawn sampling")]
    public int attemptsPerMonster = 80;
    public float sampleRadius = 2.0f;
    public float spawnY = 0.1f;
    public float minDistanceFromSafeZones = 1.2f;

    [Header("Combat")]
    public bool ensureMonsterCombatComponents = true;
    public int damagePerTick = 1;
    public float damageInterval = 0.75f;
    public float attackRange = 1.1f;

    [Header("Avoid Lit area")]
    public string litAreaName = "Lit";

    int darkAreaMask;
    bool spawnedOnce;

    void OnEnable()
    {
        if (roads != null) roads.OnGenerated += OnRoadsGenerated;
    }

    void OnDisable()
    {
        if (roads != null) roads.OnGenerated -= OnRoadsGenerated;
    }

    void Start()
    {
        // Always try to spawn on Play, even if we miss OnGenerated.
        StartCoroutine(SpawnWhenReady());
    }

    void OnRoadsGenerated()
    {
        // When you press R, this will fire. Re-spawn after regen.
        spawnedOnce = false;
        StopAllCoroutines();
        StartCoroutine(SpawnWhenReady());
    }

    System.Collections.IEnumerator SpawnWhenReady()
    {
        // 1) Wait until the map exists
        while (roads == null || roads.Map == null)
            yield return null;

        // 2) Wait a couple frames so any NavMesh baker can build
        yield return null;
        yield return null;

        // Build dark area mask (everything except Lit)
        int lit = NavMesh.GetAreaFromName(litAreaName);
        int litMask = (lit < 0) ? 0 : (1 << lit);
        darkAreaMask = NavMesh.AllAreas & ~litMask;

        // 3) Wait until a DARK navmesh sample succeeds somewhere inside city bounds
        Bounds b = GetCityBoundsWorld();
        bool navReady = false;

        for (int tries = 0; tries < 240; tries++) // up to ~4 seconds at 60fps
        {
            Vector3 guess = new Vector3(
                Random.Range(b.min.x, b.max.x),
                b.center.y,
                Random.Range(b.min.z, b.max.z)
            );

            if (NavMesh.SamplePosition(guess, out _, 5f, darkAreaMask))
            {
                navReady = true;
                break;
            }

            yield return null;
        }

        if (!navReady) yield break;

        // 4) Spawn once
        if (!spawnedOnce)
        {
            Spawn();
            spawnedOnce = true;
        }
    }

    public void Spawn()
    {
        if (roads == null || roads.Map == null) return;
        if (monsterPrefab == null) return;

        if (monstersRoot == null) monstersRoot = transform;

        int lit = NavMesh.GetAreaFromName(litAreaName);
        int litMask = (lit < 0) ? 0 : (1 << lit);
        darkAreaMask = NavMesh.AllAreas & ~litMask;

        ClearChildren(monstersRoot);

        Bounds b = GetCityBoundsWorld();

        for (int i = 0; i < monsterCount; i++)
        {
            if (TryFindDarkPoint(b, out Vector3 p))
            {
                var go = Instantiate(monsterPrefab, p + Vector3.up * spawnY, Quaternion.identity, monstersRoot);

                if (ensureMonsterCombatComponents)
                    EnsureMonsterCombat(go);

                // Scale the whole monster root
                float s = Mathf.Max(0.0001f, monsterScale);
                go.transform.localScale = Vector3.one * s;

                var agent = go.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.areaMask = darkAreaMask;

                    if (scaleNavMeshAgent)
                    {
                        agent.radius *= s;
                        agent.height *= s;

                        // Optional: keep these proportional too
                        agent.baseOffset *= s;
                        agent.speed *= 1f; // leave as-is for now
                    }

                    // Ensure it snaps onto the navmesh at the final scale
                    agent.Warp(p);
                }
            }
        }
    }

    void EnsureMonsterCombat(GameObject monster)
    {
        if (monster == null) return;

        var wander = monster.GetComponent<MonsterWander>();
        if (wander == null)
            wander = monster.AddComponent<MonsterWander>();

        wander.playerSeparationDistance = Mathf.Min(wander.playerSeparationDistance, 0.12f);

        var damage = monster.GetComponent<MonsterDamage>();
        if (damage == null)
            damage = monster.AddComponent<MonsterDamage>();

        damage.damagePerTick = Mathf.Max(1, damagePerTick);
        damage.damageInterval = Mathf.Max(0.1f, damageInterval);
        damage.attackRange = Mathf.Max(0.25f, attackRange);
    }

    bool TryFindDarkPoint(Bounds b, out Vector3 point)
    {
        for (int k = 0; k < attemptsPerMonster; k++)
        {
            float x = Random.Range(b.min.x, b.max.x);
            float z = Random.Range(b.min.z, b.max.z);
            Vector3 guess = new Vector3(x, b.center.y, z);

            if (NavMesh.SamplePosition(guess, out NavMeshHit hit, sampleRadius, darkAreaMask))
            {
                if (IsNearSafeZone(hit.position))
                    continue;

                point = hit.position;
                return true;
            }
        }

        point = Vector3.zero;
        return false;
    }

    bool IsNearSafeZone(Vector3 position)
    {
        float minDistance = Mathf.Max(0f, minDistanceFromSafeZones);
        if (minDistance <= 0f)
            return SafeZoneRegistry.IsPositionSafe(position);

        if (SafeZoneRegistry.IsPositionSafe(position))
            return true;

        const int ringSamples = 8;
        for (int i = 0; i < ringSamples; i++)
        {
            float angle = (Mathf.PI * 2f * i) / ringSamples;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * minDistance;
            if (SafeZoneRegistry.IsPositionSafe(position + offset))
                return true;
        }

        return false;
    }

    Bounds GetCityBoundsWorld()
    {
        Vector3 a = roads.GridToWorld(0, 0);
        Vector3 c = roads.GridToWorld(roads.Width - 1, roads.Height - 1);

        Vector3 min = Vector3.Min(a, c);
        Vector3 max = Vector3.Max(a, c);

        min.y -= 5f;
        max.y += 5f;

        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }
}
