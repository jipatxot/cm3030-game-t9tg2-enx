using UnityEngine;

public class ParkAndTreeGenerator : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform zoneRoot;

    [Header("Tiles")]
    public GameObject parkTilePrefab;
    public GameObject bigLotTilePrefab;
    public GameObject buildingLotTilePrefab;

    public bool clearBeforeSpawn = true;

    [Header("Cover the whole plane")]
    public bool coverAllNonRoadNonPath = true;

    [Header("Expand zones outward into nearby empty cells")]
    public bool expandZonesToRoads = true;
    [Range(1, 4)] public int expandRadius = 1;

    [Header("Layers")]
    public string walkableLayerName = "Walkable";
    public string obstacleLayerName = "IgnoreNavMesh";

    [Header("Trees")]
    public GameObject treePrefab;
    [Range(0f, 1f)] public float treeChance = 0.18f;
    public float treeYRotationRandom = 360f;

    [Header("Tree placement bias")]
    [Range(0f, 1f)] public float roadEdgeBias = 0.55f;
    [Range(0f, 1f)] public float parkCenterBias = 0.45f;
    public int distSearchRadius = 8;

    [Header("Spawn rotation (fix for Quad tiles)")]
    public bool rotateTilesFlat = true;     // set true if your tile prefabs are Unity Quad
    public float tileXRotation = 90f;       // Quad lies vertical by default, rotate to lie on ground

    int walkableLayer;
    int obstacleLayer;

    void OnEnable()
    {
        if (roads != null)
            roads.OnGenerated += Generate;
    }

    void OnDisable()
    {
        if (roads != null)
            roads.OnGenerated -= Generate;
    }

    [ContextMenu("Generate Parks And Trees")]
    public void Generate()
    {
        if (roads == null || roads.Map == null) return;

        if (zoneRoot == null) zoneRoot = transform;

        if (clearBeforeSpawn) ClearChildren(zoneRoot);

        walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);

        int w = roads.Width;
        int h = roads.Height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = roads.Map[x, y];

                // Roads and paths are spawned by RoadPathGenerator.
                if (c == RoadPathGenerator.CellType.Road) continue;
                if (c == RoadPathGenerator.CellType.Path) continue;

                var tileType = ChooseTileTypeForCell(x, y);

                if (tileType == TileType.Park)
                    SpawnTile(parkTilePrefab, x, y);
                else if (tileType == TileType.BigLot)
                    SpawnTile(bigLotTilePrefab, x, y);
                else if (tileType == TileType.BuildingLot)
                    SpawnTile(buildingLotTilePrefab, x, y);

                // Trees only inside original park cells
                if (c == RoadPathGenerator.CellType.Park && treePrefab != null)
                    TrySpawnTree(x, y);
            }
        }
    }

    enum TileType { None, Park, BigLot, BuildingLot }

    TileType ChooseTileTypeForCell(int x, int y)
    {
        var c = roads.Map[x, y];

        if (c == RoadPathGenerator.CellType.Park) return TileType.Park;
        if (c == RoadPathGenerator.CellType.BigLot) return TileType.BigLot;

        if (!coverAllNonRoadNonPath) return TileType.None;

        if (expandZonesToRoads)
        {
            if (NearCellType(x, y, RoadPathGenerator.CellType.Park, expandRadius))
                return TileType.Park;

            if (NearCellType(x, y, RoadPathGenerator.CellType.BigLot, expandRadius))
                return TileType.BigLot;
        }

        return TileType.BuildingLot;
    }

    bool NearCellType(int sx, int sy, RoadPathGenerator.CellType target, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > radius) continue;

                int x = sx + dx;
                int y = sy + dy;

                if (x < 0 || y < 0 || x >= roads.Width || y >= roads.Height) continue;

                if (roads.Map[x, y] == target)
                    return true;
            }
        }
        return false;
    }

    void SpawnTile(GameObject prefab, int x, int y)
    {
        if (prefab == null) return;

        Vector3 pos = roads.GridToWorld(x, y);

        Quaternion rot = Quaternion.identity;
        if (rotateTilesFlat)
            rot = Quaternion.Euler(tileXRotation, 0f, 0f);

        var go = Instantiate(prefab, pos, rot, zoneRoot);

        if (walkableLayer >= 0)
            SetLayerRecursively(go, walkableLayer);
    }

    void TrySpawnTree(int x, int y)
    {
        float w = TreeWeight(x, y);
        float chance = treeChance * w;

        if (Random.value > chance) return;

        Vector3 pos = roads.GridToWorld(x, y);
        var rot = Quaternion.Euler(0f, Random.Range(0f, treeYRotationRandom), 0f);

        var tree = Instantiate(treePrefab, pos, rot, zoneRoot);

        if (obstacleLayer >= 0)
            SetLayerRecursively(tree, obstacleLayer);
    }

    float TreeWeight(int x, int y)
    {
        int distToRoad = FindDistToCellType(x, y, RoadPathGenerator.CellType.Road, distSearchRadius);
        int distToParkEdge = FindDistToParkEdge(x, y, distSearchRadius);

        float nearRoad = 1f - Mathf.Clamp01(distToRoad / (float)distSearchRadius);
        float deepInPark = Mathf.Clamp01(distToParkEdge / (float)distSearchRadius);

        float a = Mathf.Lerp(1f, nearRoad, roadEdgeBias);
        float b = Mathf.Lerp(1f, deepInPark, parkCenterBias);

        return Mathf.Clamp01((a + b) * 0.5f);
    }

    int FindDistToCellType(int sx, int sy, RoadPathGenerator.CellType target, int radius)
    {
        int best = radius + 1;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = sx + dx;
                int y = sy + dy;

                if (x < 0 || y < 0 || x >= roads.Width || y >= roads.Height) continue;
                if (roads.Map[x, y] != target) continue;

                int d = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (d < best) best = d;
            }
        }

        return best;
    }

    int FindDistToParkEdge(int sx, int sy, int radius)
    {
        for (int r = 1; r <= radius; r++)
        {
            bool edgeFound = false;

            for (int dx = -r; dx <= r; dx++)
            {
                int x1 = sx + dx;
                int y1 = sy + r;
                int y2 = sy - r;

                if (!IsPark(x1, y1) || !IsPark(x1, y2))
                    edgeFound = true;
            }

            for (int dy = -r; dy <= r; dy++)
            {
                int x1 = sx + r;
                int x2 = sx - r;
                int y1 = sy + dy;

                if (!IsPark(x1, y1) || !IsPark(x2, y1))
                    edgeFound = true;
            }

            if (edgeFound)
                return r;
        }

        return 0;
    }

    bool IsPark(int x, int y)
    {
        if (x < 0 || y < 0 || x >= roads.Width || y >= roads.Height) return false;
        return roads.Map[x, y] == RoadPathGenerator.CellType.Park;
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;

        obj.layer = layer;

        foreach (Transform t in obj.transform)
            SetLayerRecursively(t.gameObject, layer);
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
