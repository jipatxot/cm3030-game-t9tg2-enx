using UnityEngine;
using UnityEngine.AI;

public class BigLotLandmarkPlacer : MonoBehaviour
{
    public enum LandmarkShape
    {
        LWithTower,
        TWithTower,
        UWithTower,
        CourtyardTower
    }

    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform buildingsRoot;
    public GameObject buildingPrefab;

    [Header("Spawn control")]
    [Range(0f, 1f)] public float buildChance = 1f;
    public int maxBuildings = 100;
    public bool clearBeforeSpawn = true;

    [Header("Allowed big-lot sizes")]
    public bool allow2x2 = true;
    public bool allow3x3 = true;
    public bool allow4x4 = true;
    public bool allow5x5 = true;

    [Header("Floors by footprint")]
    public Vector2Int floors2x2 = new Vector2Int(2, 4);
    public Vector2Int floors3x3 = new Vector2Int(3, 6);
    public Vector2Int floors4x4 = new Vector2Int(4, 7);
    public Vector2Int floors5x5 = new Vector2Int(5, 8);
    public int extraTowerFloors = 2;

    [Header("Footprint sizing")]
    [Range(0.6f, 1f)] public float fillPerTile = 0.90f;
    public float sidewalkInsetWorld = 0.25f;
    public float floorHeight = 0.35f;
    public float minHeight = 0.6f;

    [Header("Shape spacing")]
    [Tooltip("Small gap between landmark pieces so they do not visually merge.")]
    public float wingGapWorld = 0.12f;

    [Tooltip("Minimum tower width/depth so pitched roofs sit better.")]
    public float minTowerSize = 1.15f;

    [Tooltip("Only force pitched roof if tower is at least this big in both width and depth.")]
    public float minSizeForForcedPitchedRoof = 1.35f;

    [Header("Shape weights")]
    [Min(0f)] public float weightL = 1f;
    [Min(0f)] public float weightT = 1f;
    [Min(0f)] public float weightU = 1f;
    [Min(0f)] public float weightCourtyard = 1f;

    [Header("Variation")]
    public int seedOffset = 5000;
    public bool randomRotate90 = true;

    [Header("Physics / NavMesh")]
    public string obstacleLayerName = "IgnoreNavMesh";
    public bool addBoxCollider = true;
    public bool addNavMeshObstacle = true;
    public bool obstacleCarve = true;
    public float obstacleInsetWorld = 0.4f;
    public float carveMoveThreshold = 0.1f;
    public float carveTimeToStationary = 0f;

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

    [ContextMenu("Spawn Big Lot Landmarks")]
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

        int baseSeed = Mathf.Max(1, roads.seed);
        rng = new System.Random(unchecked(baseSeed + seedOffset));

        int spawned = 0;

        for (int size = 5; size >= 2; size--)
        {
            if (spawned >= maxBuildings) break;
            if (!IsSizeAllowed(size)) continue;

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    if (spawned >= maxBuildings) break;
                    if (occupied[x, y]) continue;
                    if (NextFloat() > buildChance) continue;

                    if (!CanPlaceBigLotFootprint(x, y, size, size)) continue;

                    PlaceLandmark(x, y, size, size, GetFloorsForSize(size));
                    spawned++;
                }
            }
        }
    }

    bool IsSizeAllowed(int size)
    {
        switch (size)
        {
            case 2: return allow2x2;
            case 3: return allow3x3;
            case 4: return allow4x4;
            case 5: return allow5x5;
            default: return false;
        }
    }

    Vector2Int GetFloorsForSize(int size)
    {
        switch (size)
        {
            case 2: return floors2x2;
            case 3: return floors3x3;
            case 4: return floors4x4;
            case 5: return floors5x5;
            default: return new Vector2Int(2, 4);
        }
    }

    void PlaceLandmark(int gx, int gy, int sx, int sy, Vector2Int floorsRange)
    {
        MarkOccupied(gx, gy, sx, sy);

        int floors = Rand(floorsRange.x, floorsRange.y);

        float width = FootprintWorld(sx);
        float depth = FootprintWorld(sy);
        float height = Mathf.Max(minHeight, floors * floorHeight);

        Vector3 center = ClusterCenterWorld(gx, gy, sx, sy);

        GameObject root = new GameObject($"BigLotLandmark_{sx}x{sy}");
        root.transform.SetParent(buildingsRoot, false);
        root.transform.position = center;
        root.transform.rotation = randomRotate90
            ? Quaternion.Euler(0f, 90f * Rand(0, 3), 0f)
            : Quaternion.identity;

        LandmarkShape shape = PickShape();
        BuildShape(root.transform, shape, width, depth, height, floors);

        float halfDiagonal = Mathf.Sqrt(width * width + depth * depth) * 0.5f;
        float radius = Mathf.Max(1.5f, halfDiagonal + 0.5f);

        var zone = root.AddComponent<BuildingSafeZone>();
        zone.safeRadius = radius;
    }

    void BuildShape(Transform root, LandmarkShape shape, float width, float depth, float height, int floors)
    {
        switch (shape)
        {
            case LandmarkShape.LWithTower:
                BuildLWithTower(root, width, depth, height, floors);
                break;

            case LandmarkShape.TWithTower:
                BuildTWithTower(root, width, depth, height, floors);
                break;

            case LandmarkShape.UWithTower:
                BuildUWithTower(root, width, depth, height, floors);
                break;

            default:
                BuildCourtyardTower(root, width, depth, height, floors);
                break;
        }
    }

    void BuildLWithTower(Transform root, float width, float depth, float height, int floors)
    {
        float gap = wingGapWorld;

        float longW = width * 0.58f;
        float longD = depth * 0.24f;

        float sideW = width * 0.24f;
        float sideD = depth * 0.56f;

        float towerW = Mathf.Max(minTowerSize, width * 0.24f);
        float towerD = Mathf.Max(minTowerSize, depth * 0.24f);

        float xA = -(width * 0.5f) + (longW * 0.5f);
        float zA = (depth * 0.5f) - (longD * 0.5f);

        float xB = -(width * 0.5f) + (sideW * 0.5f);
        float zB = (depth * 0.5f) - (sideD * 0.5f);

        xA -= gap * 0.25f;
        zA += gap * 0.25f;
        xB -= gap * 0.25f;
        zB += gap * 0.25f;

        SpawnPiece(root, new Vector3(xA, 0f, zA), longW, longD, height * 0.82f, Mathf.Max(1, floors - 1), false, false);
        SpawnPiece(root, new Vector3(xB, 0f, zB), sideW, sideD, height * 0.78f, Mathf.Max(1, floors - 1), true, false);
        SpawnPiece(root, new Vector3(-width * 0.10f, 0f, depth * 0.10f), towerW, towerD, height * 1.12f, floors + extraTowerFloors, false, true);
    }

    void BuildTWithTower(Transform root, float width, float depth, float height, int floors)
    {
        float gap = wingGapWorld;

        float topW = width * 0.72f;
        float topD = depth * 0.24f;

        float stemW = width * 0.24f;
        float stemD = depth * 0.52f;

        float towerW = Mathf.Max(minTowerSize, width * 0.24f);
        float towerD = Mathf.Max(minTowerSize, depth * 0.24f);

        SpawnPiece(root, new Vector3(0f, 0f, depth * 0.20f + gap * 0.15f), topW, topD, height * 0.82f, Mathf.Max(1, floors - 1), true, false);
        SpawnPiece(root, new Vector3(0f, 0f, -depth * 0.06f - gap * 0.10f), stemW, stemD, height * 0.78f, Mathf.Max(1, floors - 1), false, false);
        SpawnPiece(root, new Vector3(0f, 0f, depth * 0.02f), towerW, towerD, height * 1.14f, floors + extraTowerFloors, false, true);
    }

    void BuildUWithTower(Transform root, float width, float depth, float height, int floors)
    {
        float gap = wingGapWorld;

        float wingW = width * 0.22f;
        float wingD = depth * 0.62f;

        float backW = width * 0.56f;
        float backD = depth * 0.22f;

        float towerW = Mathf.Max(minTowerSize, width * 0.24f);
        float towerD = Mathf.Max(minTowerSize, depth * 0.24f);

        float leftX = -(width * 0.5f) + (wingW * 0.5f);
        float rightX = (width * 0.5f) - (wingW * 0.5f);
        float backZ = -(depth * 0.5f) + (backD * 0.5f);

        leftX -= gap * 0.20f;
        rightX += gap * 0.20f;
        backZ -= gap * 0.10f;

        SpawnPiece(root, new Vector3(leftX, 0f, 0f), wingW, wingD, height * 0.78f, Mathf.Max(1, floors - 1), true, false);
        SpawnPiece(root, new Vector3(rightX, 0f, 0f), wingW, wingD, height * 0.78f, Mathf.Max(1, floors - 1), false, false);
        SpawnPiece(root, new Vector3(0f, 0f, backZ), backW, backD, height * 0.74f, Mathf.Max(1, floors - 2), false, false);
        SpawnPiece(root, new Vector3(0f, 0f, depth * 0.10f), towerW, towerD, height * 1.12f, floors + extraTowerFloors, false, true);
    }

    void BuildCourtyardTower(Transform root, float width, float depth, float height, int floors)
    {
        float gap = wingGapWorld;

        float cornerW = width * 0.22f;
        float cornerD = depth * 0.22f;

        float towerW = Mathf.Max(minTowerSize, width * 0.26f);
        float towerD = Mathf.Max(minTowerSize, depth * 0.26f);

        float x = width * 0.22f + gap * 0.20f;
        float z = depth * 0.22f + gap * 0.20f;

        SpawnPiece(root, new Vector3(-x, 0f, -z), cornerW, cornerD, height * 0.70f, Mathf.Max(1, floors - 2), true, false);
        SpawnPiece(root, new Vector3(x, 0f, -z), cornerW, cornerD, height * 0.70f, Mathf.Max(1, floors - 2), false, false);
        SpawnPiece(root, new Vector3(-x, 0f, z), cornerW, cornerD, height * 0.70f, Mathf.Max(1, floors - 2), false, false);
        SpawnPiece(root, new Vector3(x, 0f, z), cornerW, cornerD, height * 0.70f, Mathf.Max(1, floors - 2), false, false);
        SpawnPiece(root, new Vector3(0f, 0f, 0f), towerW, towerD, height * 1.16f, floors + extraTowerFloors, false, true);
    }

    void SpawnPiece(
        Transform root,
        Vector3 localPos,
        float width,
        float depth,
        float height,
        int floors,
        bool addDoor,
        bool preferPitchedRoof)
    {
        var go = Instantiate(buildingPrefab, root);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;

        var vis = go.GetComponent<ProceduralBuildingVisual>();
        if (vis != null)
        {
            bool oldUsePitched = vis.usePitchedRoofForShortBuildings;
            int oldPitchedMaxFloors = vis.pitchedRoofMaxFloors;
            bool oldAddDoor = vis.addDoor;
            Vector2Int[] oldAllowed = vis.pitchedRoofAllowedFootprints;

            vis.addDoor = addDoor;

            bool canForcePitched = preferPitchedRoof &&
                                   width >= minSizeForForcedPitchedRoof &&
                                   depth >= minSizeForForcedPitchedRoof;

            if (canForcePitched)
            {
                vis.usePitchedRoofForShortBuildings = true;
                vis.pitchedRoofMaxFloors = 999;
                vis.pitchedRoofAllowedFootprints = new Vector2Int[0];
            }
            else
            {
                vis.usePitchedRoofForShortBuildings = false;
            }

            vis.Rebuild(width, depth, height, Mathf.Max(1, floors));

            vis.usePitchedRoofForShortBuildings = oldUsePitched;
            vis.pitchedRoofMaxFloors = oldPitchedMaxFloors;
            vis.addDoor = oldAddDoor;
            vis.pitchedRoofAllowedFootprints = oldAllowed;
        }
        else
        {
            go.transform.localScale = new Vector3(width, height, depth);
        }

        PostProcessPiece(go, width, depth, height);
    }

    void PostProcessPiece(GameObject go, float width, float depth, float height)
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

            float ox = Mathf.Max(0.1f, width - obstacleInsetWorld * 2f);
            float oz = Mathf.Max(0.1f, depth - obstacleInsetWorld * 2f);

            obs.size = new Vector3(ox, height, oz);
            obs.carving = obstacleCarve;
            obs.carveOnlyStationary = false;
            obs.carvingMoveThreshold = carveMoveThreshold;
            obs.carvingTimeToStationary = carveTimeToStationary;
        }
    }

    LandmarkShape PickShape()
    {
        float total = weightL + weightT + weightU + weightCourtyard;
        if (total <= 0f) return LandmarkShape.LWithTower;

        float r = NextFloat() * total;

        if (r < weightL) return LandmarkShape.LWithTower;
        r -= weightL;

        if (r < weightT) return LandmarkShape.TWithTower;
        r -= weightT;

        if (r < weightU) return LandmarkShape.UWithTower;

        return LandmarkShape.CourtyardTower;
    }

    float FootprintWorld(int tiles)
    {
        float raw = roads.tileSize * tiles * fillPerTile;
        float inset = Mathf.Max(0f, sidewalkInsetWorld) * 2f;
        return Mathf.Max(roads.tileSize * 0.2f, raw - inset);
    }

    Vector3 ClusterCenterWorld(int gx, int gy, int sx, int sy)
    {
        Vector3 a = roads.GridToWorld(gx, gy);
        Vector3 b = roads.GridToWorld(gx + sx - 1, gy + sy - 1);
        return (a + b) * 0.5f;
    }

    bool CanPlaceBigLotFootprint(int gx, int gy, int sx, int sy)
    {
        int w = roads.Width;
        int h = roads.Height;

        if (gx + sx > w) return false;
        if (gy + sy > h) return false;

        for (int oy = 0; oy < sy; oy++)
        {
            for (int ox = 0; ox < sx; ox++)
            {
                int x = gx + ox;
                int y = gy + oy;

                if (!IsBigLotCell(x, y)) return false;
                if (occupied[x, y]) return false;
            }
        }

        return true;
    }

    bool IsBigLotCell(int x, int y)
    {
        return roads.Map[x, y] == RoadPathGenerator.CellType.BigLot;
    }

    void MarkOccupied(int gx, int gy, int sx, int sy)
    {
        for (int oy = 0; oy < sy; oy++)
            for (int ox = 0; ox < sx; ox++)
                occupied[gx + ox, gy + oy] = true;
    }

    void SetLayerRecursively(Transform t, int layer)
    {
        if (t == null) return;

        t.gameObject.layer = layer;

        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
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