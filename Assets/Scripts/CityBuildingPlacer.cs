using UnityEngine;
using UnityEngine.AI;

public class CityBuildingPlacer : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform buildingsRoot;
    public GameObject buildingPrefab;

    [Header("Test run limits")]
    [Range(0f, 1f)] public float buildChance = 0.22f;
    public int maxBuildings = 120;
    public int scanStep = 1;

    [Header("Spawn area")]
    public bool clearBeforeSpawn = true;

    [Tooltip("Leave at 0 if you want buildings right up to roads. Set to 1 for a buffer.")]
    public int paddingFromRoadTiles = 0;

    [Header("Footprints")]
    public bool allow3x3 = true;
    public bool allow2x2 = true;

    [Header("Heights (floors)")]
    public Vector2Int oneByOneFloors = new Vector2Int(1, 6);
    public Vector2Int bigFootprintFloors = new Vector2Int(1, 10);

    [Header("Building scale tuning")]
    [Range(0.6f, 1f)] public float fillPerTile = 0.90f;
    public float floorHeight = 0.35f;
    public float minHeight = 0.6f;

    [Header("NavMesh blocking")]
    public string obstacleLayerName = "IgnoreNavMesh";
    public bool addNavMeshObstacle = true;
    public bool obstacleCarve = true;
    public float carveMoveThreshold = 0.1f;
    public float carveTimeToStationary = 0.0f;

    [Header("Physics blocking")]
    public bool addBoxCollider = true;

    bool[,] occupied;
    int obstacleLayer;

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

        var candidates = new System.Collections.Generic.List<Vector2Int>(w * h);

        for (int y = 1; y < h - 1; y += Mathf.Max(1, scanStep))
        {
            for (int x = 1; x < w - 1; x += Mathf.Max(1, scanStep))
            {
                if (Random.value > buildChance) continue;
                if (IsBuildableCell(x, y))
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Random.Range(i, candidates.Count);
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

            if (allow3x3 && CanPlaceFootprint(x, y, 3, 3))
            {
                PlaceCluster(x, y, 3, 3);
                spawned++;
                continue;
            }

            if (allow2x2 && CanPlaceFootprint(x, y, 2, 2))
            {
                PlaceCluster(x, y, 2, 2);
                spawned++;
                continue;
            }

            PlaceSingle(x, y);
            spawned++;
        }
    }

    void PlaceSingle(int gx, int gy)
    {
        MarkOccupied(gx, gy, 1, 1);

        int floors = Random.Range(oneByOneFloors.x, oneByOneFloors.y + 1);

        float width = roads.tileSize * fillPerTile;
        float depth = roads.tileSize * fillPerTile;
        float height = Mathf.Max(minHeight, floors * floorHeight);

        SpawnOneBuilding(gx, gy, width, depth, height, floors);
    }

    void PlaceCluster(int gx, int gy, int sx, int sy)
    {
        MarkOccupied(gx, gy, sx, sy);

        int floors = Random.Range(bigFootprintFloors.x, bigFootprintFloors.y + 1);

        float width = roads.tileSize * sx * fillPerTile;
        float depth = roads.tileSize * sy * fillPerTile;
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
            obs.size = new Vector3(width, height, depth);

            obs.carving = obstacleCarve;
            obs.carveOnlyStationary = false;
            obs.carvingMoveThreshold = carveMoveThreshold;
            obs.carvingTimeToStationary = carveTimeToStationary;
        }
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
        if (roads.Map[x, y] != RoadPathGenerator.CellType.Empty) return false;

        if (paddingFromRoadTiles <= 0) return true;

        int r = paddingFromRoadTiles;

        for (int ix = x - r; ix <= x + r; ix++)
        {
            for (int iy = y - r; iy <= y + r; iy++)
            {
                if (ix < 0 || iy < 0 || ix >= roads.Width || iy >= roads.Height) continue;
                if (roads.Map[ix, iy] == RoadPathGenerator.CellType.Road) return false;
                if (roads.Map[ix, iy] == RoadPathGenerator.CellType.Path) return false;
            }
        }

        return true;
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
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }
}