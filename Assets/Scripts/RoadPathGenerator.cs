using System;
using System.Collections.Generic;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

public class RoadPathGenerator : MonoBehaviour
{
    [Header("NavMesh layers")]
    public string walkableLayerName = "Walkable";
    int walkableLayer;

    [Header("Grid")]
    public int width = 44;
    public int height = 44;
    public float tileSize = 1f;

    [Header("Random")]
    public int seed = 0;                 // 0 = random each run
    public bool useRandomSeed = true;

    [Header("Blocks")]
    public int minBlockSize = 4;
    public int maxBlockSize = 7;
    public int roadWidth = 1;

    [Header("Paths")]
    [Range(0f, 1f)] public float pathChance = 0.45f;
    public int minShortcutLen = 10;
    public int maxShortcutLen = 28;
    public int shortcutAttemptsDiv = 180;

    [Header("Extra streets")]
    [Range(0f, 1f)] public float extraStreetChance = 0.12f;

    [Header("Zones")]
    [Range(0f, 1f)] public float parkChancePerBlock = 0.18f;
    [Range(0f, 1f)] public float bigLotChancePerBlock = 0.22f;
    public int minZonePaddingFromRoad = 1;

    [Header("Spawn roads and paths")]
    public Transform roadRoot;
    public GameObject roadTilePrefab;
    public GameObject pathTilePrefab;
    public bool clearBeforeSpawn = true;

    [Header("Placement")]
    public bool centerOnCity = true;
    public Vector3 worldOffset = Vector3.zero;
    public float yOffset = 0.05f;

    [Header("Auto size from ground")]
    public Renderer groundRenderer;
    public bool autoFitToGround = true;
    public int paddingTiles = 2;

    [Header("Controls")]
    public bool generateOnStart = true;

    [Header("Spawn rotation (fix for Quad tiles)")]
    public bool rotateTilesFlat = true;   // set true if your tile prefabs are Unity Quad
    public float tileXRotation = 90f;

    public enum CellType { Empty, Road, Path, Park, BigLot }

    public CellType[,] Map { get; private set; }
    public int Width => width;
    public int Height => height;

    public event Action OnGenerated;

    System.Random rng;

    void Start()
    {
        if (UnityEngine.Application.isPlaying && generateOnStart)
            GenerateAndSpawn();
    }

    // Convenience for other scripts.
    public void Generate() => GenerateAndSpawn();

    [ContextMenu("Generate And Spawn")]
    public void GenerateAndSpawn()
    {
        if (!roadRoot || !roadTilePrefab || !pathTilePrefab)
        {
            UnityEngine.Debug.LogError("Assign roadRoot, roadTilePrefab, pathTilePrefab.");
            return;
        }

        int s = useRandomSeed ? UnityEngine.Random.Range(1, int.MaxValue) : Mathf.Max(1, seed);
        rng = new System.Random(s);

        walkableLayer = LayerMask.NameToLayer(walkableLayerName);

        AutoFitGridToGround();

        Map = new CellType[width, height];

        BuildPrimaryStreetGrid();
        AddExtraStreetsInsideBlocks();
        MarkZonesInsideBlocks();
        AddRoadToRoadShortcuts();

        SpawnRoadAndPathTiles();

        OnGenerated?.Invoke();
    }

    void AutoFitGridToGround()
    {
        if (!autoFitToGround || groundRenderer == null) return;

        Bounds b = groundRenderer.bounds;

        float sizeX = b.size.x;
        float sizeZ = b.size.z;

        int w = Mathf.FloorToInt(sizeX / tileSize) + paddingTiles * 2;
        int h = Mathf.FloorToInt(sizeZ / tileSize) + paddingTiles * 2;

        width = Mathf.Max(10, w);
        height = Mathf.Max(10, h);

        Vector3 p = transform.position;
        transform.position = new Vector3(b.center.x, p.y, b.center.z);
    }

    void BuildPrimaryStreetGrid()
    {
        int x = 0;
        while (x < width)
        {
            int block = Rand(minBlockSize, maxBlockSize);
            int roadX = x + block;

            if (roadX < width)
                PaintVerticalRoad(roadX, 0, height - 1);

            x = roadX + roadWidth;
        }

        int y = 0;
        while (y < height)
        {
            int block = Rand(minBlockSize, maxBlockSize);
            int roadY = y + block;

            if (roadY < height)
                PaintHorizontalRoad(roadY, 0, width - 1);

            y = roadY + roadWidth;
        }

        PaintVerticalRoad(0, 0, height - 1);
        PaintVerticalRoad(width - 1, 0, height - 1);
        PaintHorizontalRoad(0, 0, width - 1);
        PaintHorizontalRoad(height - 1, 0, width - 1);
    }

    void AddExtraStreetsInsideBlocks()
    {
        for (int x = 2; x < width - 2; x++)
        {
            if (NextFloat() > extraStreetChance) continue;
            if (!ColumnIsMostlyEmpty(x)) continue;

            PaintVerticalRoad(x, 1, height - 2);
        }

        for (int y = 2; y < height - 2; y++)
        {
            if (NextFloat() > extraStreetChance) continue;
            if (!RowIsMostlyEmpty(y)) continue;

            PaintHorizontalRoad(y, 1, width - 2);
        }
    }

    void MarkZonesInsideBlocks()
    {
        List<int> roadCols = GetRoadLineCols();
        List<int> roadRows = GetRoadLineRows();

        for (int ci = 0; ci < roadCols.Count - 1; ci++)
        {
            for (int ri = 0; ri < roadRows.Count - 1; ri++)
            {
                int left = roadCols[ci];
                int right = roadCols[ci + 1];
                int bottom = roadRows[ri];
                int top = roadRows[ri + 1];

                int x0 = left + 1 + minZonePaddingFromRoad;
                int x1 = right - 1 - minZonePaddingFromRoad;
                int y0 = bottom + 1 + minZonePaddingFromRoad;
                int y1 = top - 1 - minZonePaddingFromRoad;

                if (x1 <= x0 || y1 <= y0) continue;

                float r = NextFloat();
                CellType zone = CellType.Empty;

                if (r < parkChancePerBlock) zone = CellType.Park;
                else if (r < parkChancePerBlock + bigLotChancePerBlock) zone = CellType.BigLot;

                if (zone == CellType.Empty) continue;

                for (int x = x0; x <= x1; x++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        if (Map[x, y] == CellType.Empty)
                            Map[x, y] = zone;
                    }
                }
            }
        }
    }

    void AddRoadToRoadShortcuts()
    {
        int attempts = (width * height) / Mathf.Max(60, shortcutAttemptsDiv);

        for (int i = 0; i < attempts; i++)
        {
            if (NextFloat() > pathChance) continue;

            Vector2Int a = RandomRoadCell();
            Vector2Int b = RandomRoadCell();

            int dist = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
            if (dist < minShortcutLen || dist > maxShortcutLen) continue;

            CarveLPathBetweenRoads(a, b);
        }
    }

    Vector2Int RandomRoadCell()
    {
        for (int tries = 0; tries < 8000; tries++)
        {
            int x = Rand(1, width - 2);
            int y = Rand(1, height - 2);
            if (Map[x, y] == CellType.Road) return new Vector2Int(x, y);
        }
        return new Vector2Int(1, 1);
    }

    void CarveLPathBetweenRoads(Vector2Int a, Vector2Int b)
    {
        Vector2Int turn = NextBool()
            ? new Vector2Int(b.x, a.y)
            : new Vector2Int(a.x, b.y);

        CarvePathLine(a, turn);
        CarvePathLine(turn, b);
    }

    void CarvePathLine(Vector2Int from, Vector2Int to)
    {
        int x = from.x;
        int y = from.y;

        int dx = (to.x > x) ? 1 : (to.x < x ? -1 : 0);
        int dy = (to.y > y) ? 1 : (to.y < y ? -1 : 0);

        int safety = 2000;

        while ((x != to.x || y != to.y) && safety-- > 0)
        {
            if (x != to.x) x += dx;
            else if (y != to.y) y += dy;

            if (!InBounds(x, y)) break;

            if (Map[x, y] == CellType.Road) continue;

            Map[x, y] = CellType.Path;
        }
    }

    List<int> GetRoadLineCols()
    {
        var roadCols = new List<int>();

        for (int x = 0; x < width; x++)
        {
            int count = 0;
            for (int y = 0; y < height; y++)
                if (Map[x, y] == CellType.Road) count++;

            if (count > height * 0.7f)
                roadCols.Add(x);
        }

        roadCols.Sort();
        if (roadCols.Count == 0) roadCols.Add(0);
        return roadCols;
    }

    List<int> GetRoadLineRows()
    {
        var roadRows = new List<int>();

        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
                if (Map[x, y] == CellType.Road) count++;

            if (count > width * 0.7f)
                roadRows.Add(y);
        }

        roadRows.Sort();
        if (roadRows.Count == 0) roadRows.Add(0);
        return roadRows;
    }

    void PaintVerticalRoad(int x, int y0, int y1)
    {
        for (int y = y0; y <= y1; y++)
            SetRoad(x, y);
    }

    void PaintHorizontalRoad(int y, int x0, int x1)
    {
        for (int x = x0; x <= x1; x++)
            SetRoad(x, y);
    }

    void SetRoad(int x, int y)
    {
        if (!InBounds(x, y)) return;
        Map[x, y] = CellType.Road;
    }

    bool ColumnIsMostlyEmpty(int x)
    {
        int empty = 0;
        int total = 0;

        for (int y = 1; y < height - 1; y++)
        {
            total++;
            if (Map[x, y] == CellType.Empty) empty++;
        }

        return empty > total * 0.85f;
    }

    bool RowIsMostlyEmpty(int y)
    {
        int empty = 0;
        int total = 0;

        for (int x = 1; x < width - 1; x++)
        {
            total++;
            if (Map[x, y] == CellType.Empty) empty++;
        }

        return empty > total * 0.85f;
    }

    public Vector3 GridToWorld(int x, int y)
    {
        float wx = x * tileSize;
        float wz = y * tileSize;

        float totalW = (width - 1) * tileSize;
        float totalH = (height - 1) * tileSize;

        if (centerOnCity)
        {
            wx -= totalW * 0.5f;
            wz -= totalH * 0.5f;
        }

        float topY = transform.position.y;
        if (groundRenderer != null)
            topY = groundRenderer.bounds.max.y;

        return new Vector3(
            transform.position.x + wx + worldOffset.x,
            topY + yOffset,
            transform.position.z + wz + worldOffset.z
        );
    }

    void SpawnRoadAndPathTiles()
    {
        if (clearBeforeSpawn) ClearChildren(roadRoot);

        Quaternion rot = Quaternion.identity;
        if (rotateTilesFlat)
            rot = Quaternion.Euler(tileXRotation, 0f, 0f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (Map[x, y] != CellType.Road && Map[x, y] != CellType.Path) continue;

                var prefab = (Map[x, y] == CellType.Road) ? roadTilePrefab : pathTilePrefab;
                Vector3 pos = GridToWorld(x, y);

                var go = Instantiate(prefab, pos, rot, roadRoot);

                if (walkableLayer >= 0)
                    SetLayerRecursively(go, walkableLayer);
            }
        }
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

    bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    int Rand(int min, int maxInclusive) => rng.Next(min, maxInclusive + 1);
    float NextFloat() => (float)rng.NextDouble();
    bool NextBool() => rng.NextDouble() < 0.5;
}
