using UnityEngine;
using UnityEngine.AI;

public class CityBuildingPlacer : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform buildingsRoot;
    public GameObject buildingPrefab;

    [Header("Test run limits")]
    [Range(0f, 1f)] public float buildChance = 0.3f;
    public int maxBuildings = 300;
    public int scanStep = 1;

    [Header("Spawn area")]
    public bool clearBeforeSpawn = true;

    [Header("Sidewalk")]
    [Tooltip("World units. 0.25 means quarter of a tile if tileSize=1.")]
    public float sidewalkInsetWorld = 0.25f;

    [Header("Footprints")]
    public bool allow1x1 = false;
    public bool allow2x2 = true;
    public bool allow3x3 = true;
    public bool allow4x4 = true;
    public bool allow5x5 = true;

    [Header("Heights (floors) per footprint")]
    public Vector2Int floors1x1 = new Vector2Int(1, 6);
    public Vector2Int floors2x2 = new Vector2Int(2, 8);
    public Vector2Int floors3x3 = new Vector2Int(3, 10);
    public Vector2Int floors4x4 = new Vector2Int(3, 12);
    public Vector2Int floors5x5 = new Vector2Int(4, 14);

    [Header("Building scale tuning")]
    [Range(0.6f, 1f)] public float fillPerTile = 0.90f;
    public float floorHeight = 0.35f;
    public float minHeight = 0.6f;

    [Header("NavMesh obstacle sizing")]
    public float obstacleInsetWorld = 0.4f;

    [Header("NavMesh blocking")]
    public string obstacleLayerName = "IgnoreNavMesh";
    public bool addNavMeshObstacle = true;
    public bool obstacleCarve = true;
    public float carveMoveThreshold = 0.1f;
    public float carveTimeToStationary = 0.0f;

    [Header("Physics blocking")]
    public bool addBoxCollider = true;

    [Header("Deterministic seed offset")]
    public int seedOffset = 1000;

    bool[,] occupied;
    int obstacleLayer;

    System.Random rng;

    void OnEnable()
    {
        if (roads != null) roads.OnGenerated += SpawnBuildings;
    }

    void OnDisable()
    {
        if (roads != null) roads.OnGenerated -= SpawnBuildings;
    }

    [ContextMenu("Spawn Buildings")]
    public void SpawnBuildings()
    {
        if (roads == null || roads.Map == null) return;
        if (buildingPrefab == null) return;

        if (buildingsRoot == null) buildingsRoot = transform;
        if (clearBeforeSpawn) ClearChildren(buildingsRoot);

        obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);

        int w = roads.Width;
        int h = roads.Height;

        occupied = new bool[w, h];

        // Deterministic RNG for buildings, based on the world seed.
        int baseSeed = Mathf.Max(1, roads.seed);
        rng = new System.Random(unchecked(baseSeed + seedOffset));

        var candidates = new System.Collections.Generic.List<Vector2Int>(w * h);

        for (int y = 1; y < h - 1; y += Mathf.Max(1, scanStep))
        {
            for (int x = 1; x < w - 1; x += Mathf.Max(1, scanStep))
            {
                if (NextFloat() > buildChance) continue;
                if (IsBuildableCell(x, y))
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        // Fisher-Yates shuffle using the same seeded RNG.
        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Rand(i, candidates.Count - 1);
            var tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        int spawned = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (spawned >= maxBuildings) break;

            int x = candidates[i].x;
            int y = candidates[i].y;

            if (occupied[x, y]) continue;

            if (allow5x5 && CanPlaceFootprint(x, y, 5, 5))
            {
                PlaceCluster(x, y, 5, 5, floors5x5);
                spawned++;
                continue;
            }

            if (allow4x4 && CanPlaceFootprint(x, y, 4, 4))
            {
                PlaceCluster(x, y, 4, 4, floors4x4);
                spawned++;
                continue;
            }

            if (allow3x3 && CanPlaceFootprint(x, y, 3, 3))
            {
                PlaceCluster(x, y, 3, 3, floors3x3);
                spawned++;
                continue;
            }

            if (allow2x2 && CanPlaceFootprint(x, y, 2, 2))
            {
                PlaceCluster(x, y, 2, 2, floors2x2);
                spawned++;
                continue;
            }

            if (allow1x1 && IsBuildableCell(x, y))
            {
                PlaceSingle(x, y);
                spawned++;
            }
        }
    }

    void PlaceSingle(int gx, int gy)
    {
        MarkOccupied(gx, gy, 1, 1);

        int floors = Rand(floors1x1.x, floors1x1.y);

        float width = FootprintWorld(1);
        float depth = FootprintWorld(1);
        float height = Mathf.Max(minHeight, floors * floorHeight);

        SpawnOneBuilding(gx, gy, width, depth, height, floors);
    }

    void PlaceCluster(int gx, int gy, int sx, int sy, Vector2Int floorsRange)
    {
        MarkOccupied(gx, gy, sx, sy);

        int floors = Rand(floorsRange.x, floorsRange.y);

        float width = FootprintWorld(sx);
        float depth = FootprintWorld(sy);
        float height = Mathf.Max(minHeight, floors * floorHeight);

        Vector3 pos = ClusterCenterWorld(gx, gy, sx, sy);
        var go = Instantiate(buildingPrefab, pos, Quaternion.identity, buildingsRoot);

        var vis = go.GetComponent<ProceduralBuildingVisual>();
        if (vis != null)
            vis.Rebuild(width, depth, height, floors);
        else
            go.transform.localScale = new Vector3(width, height, depth);

        PostProcessBuilding(go, width, depth, height);
    }

    float FootprintWorld(int tiles)
    {
        float raw = roads.tileSize * tiles * fillPerTile;
        float inset = Mathf.Max(0f, sidewalkInsetWorld) * 2f;

        float clamped = Mathf.Max(roads.tileSize * 0.2f, raw - inset);
        return clamped;
    }

    Vector3 ClusterCenterWorld(int gx, int gy, int sx, int sy)
    {
        Vector3 a = roads.GridToWorld(gx, gy);
        Vector3 b = roads.GridToWorld(gx + sx - 1, gy + sy - 1);
        return (a + b) * 0.5f;
    }

    void SpawnOneBuilding(int gx, int gy, float width, float depth, float height, int floors)
    {
        Vector3 pos = roads.GridToWorld(gx, gy);
        var go = Instantiate(buildingPrefab, pos, Quaternion.identity, buildingsRoot);

        var vis = go.GetComponent<ProceduralBuildingVisual>();
        if (vis != null)
            vis.Rebuild(width, depth, height, floors);
        else
            go.transform.localScale = new Vector3(width, height, depth);

        PostProcessBuilding(go, width, depth, height);
    }

    void PostProcessBuilding(GameObject go, float width, float depth, float height)
    {
        if (go == null) return;

        EnsureBuildingLightSource(go, width, depth);

        if (obstacleLayer >= 0)
            SetLayerRecursively(go.transform, obstacleLayer);

        if (addBoxCollider)
        {
            var col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();

            col.size = new Vector3(width, height, depth);
            col.center = new Vector3(0f, height * 0.5f, 0f);
        }

        if (addNavMeshObstacle)
        {
            var obs = go.GetComponent<NavMeshObstacle>();
            if (obs == null) obs = go.AddComponent<NavMeshObstacle>();

            obs.shape = NavMeshObstacleShape.Box;
            obs.center = new Vector3(0f, height * 0.5f, 0f);

            float ox = Mathf.Max(0.1f, width - obstacleInsetWorld * 2f);
            float oz = Mathf.Max(0.1f, depth - obstacleInsetWorld * 2f);

            obs.size = new Vector3(ox, height, oz);

            obs.carving = obstacleCarve;
            obs.carveOnlyStationary = false;
            obs.carvingMoveThreshold = carveMoveThreshold;
            obs.carvingTimeToStationary = carveTimeToStationary;
        }
    }

    void EnsureBuildingLightSource(GameObject go, float width, float depth)
    {
        if (go == null) return;

        var source = go.GetComponent<LightSource>();
        if (source == null) source = go.AddComponent<LightSource>();

        source.sourceType = LightSource.LightSourceType.Building;
        float halfDiagonal = Mathf.Sqrt(width * width + depth * depth) * 0.5f;
        float radius = Mathf.Max(1.5f, halfDiagonal + 0.5f);
        source.restoreRadius = radius;
        source.safeRadius = radius;
        source.healthRestoreAmount = 2;
        source.minSecondsToBlackout = 28f;
        source.maxSecondsToBlackout = 80f;
    }

    void SetLayerRecursively(Transform t, int layer)
    {
        if (t == null) return;

        t.gameObject.layer = layer;

        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    bool CanPlaceFootprint(int gx, int gy, int sx, int sy)
    {
        int w = roads.Width;
        int h = roads.Height;

        if (gx + sx >= w - 1) return false;
        if (gy + sy >= h - 1) return false;

        for (int oy = 0; oy < sy; oy++)
        {
            for (int ox = 0; ox < sx; ox++)
            {
                int x = gx + ox;
                int y = gy + oy;

                if (!IsBuildableCell(x, y)) return false;
                if (occupied[x, y]) return false;
            }
        }

        return true;
    }

    bool IsBuildableCell(int x, int y)
    {
        return roads.Map[x, y] == RoadPathGenerator.CellType.BuildingLot;
    }

    void MarkOccupied(int gx, int gy, int sx, int sy)
    {
        for (int oy = 0; oy < sy; oy++)
            for (int ox = 0; ox < sx; ox++)
                occupied[gx + ox, gy + oy] = true;
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    float NextFloat()
    {
        return (float)rng.NextDouble();
    }

    int Rand(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) return minInclusive;
        return rng.Next(minInclusive, maxInclusive + 1);
    }
}
