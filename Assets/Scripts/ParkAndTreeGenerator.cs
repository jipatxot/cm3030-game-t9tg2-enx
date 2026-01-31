using UnityEngine;

public class ParkAndTreeGenerator : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform treeRoot;

    [Header("Trees")]
    public GameObject treePrefab;
    [Range(0f, 1f)] public float treeChance = 0.25f;
    public float treeYRotationRandom = 360f;

    [Header("Tree size by location (near roads vs park middle)")]
    [Tooltip("0 = near road edge, 1 = deep in park.")]
    public Vector2 heightScaleNearRoad = new Vector2(0.55f, 0.85f);
    public Vector2 heightScaleDeepPark = new Vector2(0.95f, 1.60f);

    [Tooltip("0 = near road edge, 1 = deep in park.")]
    public Vector2 widthScaleNearRoad = new Vector2(0.65f, 0.95f);
    public Vector2 widthScaleDeepPark = new Vector2(0.85f, 1.35f);

    [Tooltip("If true, uses the same width for X and Z.")]
    public bool keepWidthUniform = true;

    [Header("How strong is the size shift?")]
    [Range(0f, 2f)] public float sizeBiasStrength = 1.0f;

    [Header("Tree placement bias")]
    [Range(0f, 1f)] public float roadEdgeBias = 0.55f;
    [Range(0f, 1f)] public float parkCenterBias = 0.45f;
    public int distSearchRadius = 8;

    [Header("Layers")]
    public string obstacleLayerName = "IgnoreNavMesh";

    [Header("Clear")]
    public bool clearBeforeSpawn = true;

    [Header("Deterministic seed offset")]
    public int seedOffset = 2000;

    int obstacleLayer;

    System.Random rng;

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

    [ContextMenu("Generate Park Trees")]
    public void Generate()
    {
        if (roads == null || roads.Map == null) return;
        if (treePrefab == null) return;

        if (treeRoot == null) treeRoot = transform;

        if (clearBeforeSpawn) ClearChildren(treeRoot);

        obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);

        int baseSeed = Mathf.Max(1, roads.seed);
        rng = new System.Random(unchecked(baseSeed + seedOffset));

        int w = roads.Width;
        int h = roads.Height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (roads.Map[x, y] != RoadPathGenerator.CellType.Park)
                    continue;

                TrySpawnTree(x, y);
            }
        }
    }

    void TrySpawnTree(int x, int y)
    {
        float chance = treeChance * TreeWeight(x, y);
        if (NextFloat() > chance) return;

        Vector3 pos = roads.GridToWorld(x, y);
        var rot = Quaternion.Euler(0f, RandFloat(0f, treeYRotationRandom), 0f);

        var tree = Instantiate(treePrefab, pos, rot, treeRoot);

        float size01 = ParkMiddle01(x, y);
        ApplyRandomTreeScale(tree.transform, size01);

        if (obstacleLayer >= 0)
            SetLayerRecursively(tree, obstacleLayer);
    }

    float ParkMiddle01(int x, int y)
    {
        int distToRoad = FindDistToCellType(x, y, RoadPathGenerator.CellType.Road, distSearchRadius);
        int distToParkEdge = FindDistToParkEdge(x, y, distSearchRadius);

        float nearRoad01 = 1f - Mathf.Clamp01(distToRoad / (float)distSearchRadius);
        float deepInPark01 = Mathf.Clamp01(distToParkEdge / (float)distSearchRadius);

        float size01 = deepInPark01;

        size01 *= (1f - nearRoad01);
        size01 = Mathf.Clamp01(size01 * sizeBiasStrength);

        return size01;
    }

    void ApplyRandomTreeScale(Transform t, float size01)
    {
        if (t == null) return;

        float minH = Mathf.Lerp(heightScaleNearRoad.x, heightScaleDeepPark.x, size01);
        float maxH = Mathf.Lerp(heightScaleNearRoad.y, heightScaleDeepPark.y, size01);
        if (maxH < minH) { float tmp = minH; minH = maxH; maxH = tmp; }

        float minW = Mathf.Lerp(widthScaleNearRoad.x, widthScaleDeepPark.x, size01);
        float maxW = Mathf.Lerp(widthScaleNearRoad.y, widthScaleDeepPark.y, size01);
        if (maxW < minW) { float tmp = minW; minW = maxW; maxW = tmp; }

        float h = RandFloat(minH, maxH);

        float wx = RandFloat(minW, maxW);
        float wz = keepWidthUniform ? wx : RandFloat(minW, maxW);

        t.localScale = new Vector3(wx, h, wz);
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

    float RandFloat(float minInclusive, float maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            float t = minInclusive;
            minInclusive = maxInclusive;
            maxInclusive = t;
        }

        float t01 = (float)rng.NextDouble();
        return Mathf.Lerp(minInclusive, maxInclusive, t01);
    }
}
