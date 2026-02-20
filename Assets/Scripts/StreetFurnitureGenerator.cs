using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class StreetFurnitureGenerator : MonoBehaviour
{
    public readonly List<Transform> SpawnedLamps = new List<Transform>();

    [Header("Debug")]
    public bool debugSignals = false;
    public bool pauseOnBadSignal = false;
    public float debugDrawSeconds = 30f;

    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform furnitureRoot;

    [Header("Ground snapping")]
    public Collider groundCollider;
    public LayerMask groundMask = ~0;
    public float snapStartHeight = 200f;

    [Header("Prefabs")]
    public GameObject streetLampPrefab;
    public GameObject trafficLightPrefab;
    public GameObject zebraCrossingPrefab;

    [Header("Clear")]
    public bool clearBeforeSpawn = true;

    [Header("Street Lamps")]
    [Range(0f, 1f)] public float lampChance = 0.75f;
    public int lampSpacingTiles = 18;
    public float lampSideOffset = 0.65f;
    public float lampYOffset = 0.02f;

    [Header("Traffic Lights")]
    public int minTilesBetweenSignals = 18;
    public float signalCornerOffset = 0.75f;
    public float signalYOffset = 0.02f;

    [Header("Zebras")]
    public bool spawnZebras = true;
    [Range(0f, 1f)] public float zebraChancePerSide = 1.0f;
    [Range(0f, 1f)] public float smallRoadZebraChancePerSide = 0.25f;
    [Range(0f, 1f)] public float smallRoadSignalChance = 0.05f;
    [Range(0f, 0.49f)] public float zebraInsetFromJunction = 0.18f;
    public float zebraYOffset = 0.06f;

    [Header("Zebra rotation (quad fix)")]
    public bool rotateZebrasFlat = true;
    public float zebraXRotation = 90f;

    [Header("Lamp lit areas (NavMeshModifierVolume only)")]
    public bool createLampLitVolumes = true;
    public string litAreaName = "Lit";
    public float lampLitRadius = 3f;
    public float lampLitHeight = 1.5f;
    public float lampLitCenterY = 1.0f;

    [Header("Traffic light lit areas (NavMeshModifierVolume)")]
    public bool createTrafficLightLitVolumes = true;
    public string trafficLightLitAreaName = "Lit";
    public float trafficLightLitRadius = 1f;
    public float trafficLightLitHeight = 1.5f;
    public float trafficLightLitCenterY = 1.0f;
    public string trafficLightLitChildName = "LitVolume_TrafficLight";

    [Header("Layers")]
    public string walkableLayerName = "Walkable";
    public string obstacleLayerName = "IgnoreNavMesh";
    public bool forceZebrasWalkableLayer = true;

    [Header("Safety")]
    public int maxJunctionClusterSize = 200;

    [Header("Deterministic seed offset")]
    public int seedOffset = 3000;

    int walkableLayer = -1;
    int obstacleLayer = -1;

    bool[,] hasSignal;
    HashSet<int> zebraKeys = new HashSet<int>();

    System.Random rng;

    void Awake()
    {
        walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
    }

    void OnEnable()
    {
        if (roads != null) roads.OnGenerated += Generate;
    }

    void OnDisable()
    {
        if (roads != null) roads.OnGenerated -= Generate;
    }

    [ContextMenu("Generate Street Furniture")]
    public void Generate()
    {
        if (roads == null || roads.Map == null) return;

        if (furnitureRoot == null) furnitureRoot = transform;

        if (clearBeforeSpawn) ClearChildren(furnitureRoot);

        SpawnedLamps.Clear();
        zebraKeys.Clear();

        int baseSeed = Mathf.Max(1, roads.seed);
        rng = new System.Random(unchecked(baseSeed + seedOffset));

        int w = roads.Width;
        int h = roads.Height;

        hasSignal = new bool[w, h];

        SpawnLamps(w, h);
        SpawnSignals(w, h);

        if (spawnZebras && zebraCrossingPrefab != null)
            SpawnZebrasFromJunctionClusters(w, h);
    }

    // -----------------------------
    // Lamps
    // -----------------------------

    void SpawnLamps(int w, int h)
    {
        if (streetLampPrefab == null) return;

        int step = Mathf.Max(2, lampSpacingTiles);

        for (int x = 2; x < w - 2; x++)
        {
            for (int y = 2; y < h - 2; y++)
            {
                if (!IsRoad(x, y)) continue;
                if (!IsStraight(x, y)) continue;
                if (IsMultiLaneCorridorCell(x, y)) continue;

                if (((x * 73856093) ^ (y * 19349663)) % step != 0) continue;
                if (NextFloat() > lampChance) continue;

                Vector3 basePos = roads.GridToWorld(x, y);
                Vector3 side = ChooseRoadSideOffset(x, y, lampSideOffset);
                if (side == Vector3.zero) continue;

                Vector3 candidate = basePos + side;

                if (!TrySnapToGroundStrict(candidate, lampYOffset, out var pos))
                    continue;

                var lamp = Instantiate(streetLampPrefab, pos, Quaternion.identity, furnitureRoot);

                if (obstacleLayer >= 0) SetLayerRecursively(lamp, obstacleLayer);

                SpawnedLamps.Add(lamp.transform);

                if (createLampLitVolumes)
                    EnsureLampLitVolume(lamp);

                EnsureLampSafeZone(lamp);
            }
        }
    }

    void EnsureLampSafeZone(GameObject lamp)
    {
        if (lamp == null) return;

        var zone = lamp.GetComponent<LampSafeZone>();
        if (zone == null) zone = lamp.AddComponent<LampSafeZone>();

        zone.safeRadius = Mathf.Max(0.5f, lampLitRadius);
    }


    void EnsureTrafficLightLitVolume(GameObject trafficLight)
    {
        if (trafficLight == null) return;
        if (!createTrafficLightLitVolumes) return;

        int area = NavMesh.GetAreaFromName(trafficLightLitAreaName);
        if (area < 0) return;

        Transform t = trafficLight.transform.Find(trafficLightLitChildName);
        if (t == null)
        {
            var go = new GameObject(trafficLightLitChildName);
            t = go.transform;
            t.SetParent(trafficLight.transform, false);
        }

        if (walkableLayer >= 0) SetLayerRecursively(t.gameObject, walkableLayer);

        var vol = t.GetComponent<NavMeshModifierVolume>();
        if (vol == null) vol = t.gameObject.AddComponent<NavMeshModifierVolume>();

        vol.area = area;
        vol.size = new Vector3(trafficLightLitRadius * 2f, trafficLightLitHeight, trafficLightLitRadius * 2f);
        vol.center = new Vector3(0f, trafficLightLitCenterY, 0f);
    }

    void EnsureLampLitVolume(GameObject lamp)
    {
        if (lamp == null) return;

        int litArea = NavMesh.GetAreaFromName(litAreaName);
        if (litArea < 0) return;

        Transform t = lamp.transform.Find("LitVolume");
        if (t == null)
        {
            var go = new GameObject("LitVolume");
            t = go.transform;
            t.SetParent(lamp.transform, false);
        }

        if (walkableLayer >= 0) SetLayerRecursively(t.gameObject, walkableLayer);

        var vol = t.GetComponent<NavMeshModifierVolume>();
        if (vol == null) vol = t.gameObject.AddComponent<NavMeshModifierVolume>();

        vol.area = litArea;
        vol.size = new Vector3(lampLitRadius * 2f, lampLitHeight, lampLitRadius * 2f);
        vol.center = new Vector3(0f, lampLitCenterY, 0f);
    }

    // -----------------------------
    // Traffic lights (clusters)
    // -----------------------------

    void SpawnSignals(int w, int h)
    {
        if (trafficLightPrefab == null) return;

        bool[,] isJunctionCell = new bool[w, h];
        bool[,] visited = new bool[w, h];

        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                if (!IsRoad(x, y)) continue;

                bool hasH = IsRoad(x - 1, y) || IsRoad(x + 1, y);
                bool hasV = IsRoad(x, y - 1) || IsRoad(x, y + 1);
                if (!(hasH && hasV)) continue;

                if (RoadNeighborCount(x, y) < 3) continue;

                isJunctionCell[x, y] = true;
            }
        }

        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                if (!isJunctionCell[x, y]) continue;
                if (visited[x, y]) continue;

                var cluster = FloodCluster(x, y, isJunctionCell, visited, w, h);
                if (cluster.Count == 0) continue;

                bool isBoulevardIntersection = ClusterTouchesBoulevard(cluster);
                Vector2Int anchor = cluster[0];

                bool shouldPlace =
                    isBoulevardIntersection
                        ? true
                        : (NextFloat() <= smallRoadSignalChance && FarFromOtherSignals(anchor.x, anchor.y));

                if (!shouldPlace) continue;

                hasSignal[anchor.x, anchor.y] = true;
                SpawnTrafficLightsForCluster(cluster);
            }
        }
    }

    void SpawnTrafficLightsForCluster(List<Vector2Int> cluster)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        for (int i = 0; i < cluster.Count; i++)
        {
            int x = cluster[i].x;
            int y = cluster[i].y;

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        int cx = (minX + maxX) / 2;
        int cy = (minY + maxY) / 2;

        Vector3 c = roads.GridToWorld(cx, cy);

        TryPlaceSignalAtCorner(c, cx, cy, +1, +1);
        TryPlaceSignalAtCorner(c, cx, cy, -1, +1);
        TryPlaceSignalAtCorner(c, cx, cy, +1, -1);
        TryPlaceSignalAtCorner(c, cx, cy, -1, -1);
    }

    void TryPlaceSignalAtCorner(Vector3 center, int x, int y, int sx, int sy)
    {
        int cornerX = x + sx;
        int cornerY = y + sy;

        int roadAX = x + sx;
        int roadAY = y;

        int roadBX = x;
        int roadBY = y + sy;

        Vector3 candidate = center + new Vector3(sx * signalCornerOffset, 0f, sy * signalCornerOffset);

        bool inCorner = InBounds(cornerX, cornerY);
        bool inA = InBounds(roadAX, roadAY);
        bool inB = InBounds(roadBX, roadBY);

        bool cornerIsRoad = inCorner && IsRoad(cornerX, cornerY);
        bool aIsRoad = inA && IsRoad(roadAX, roadAY);
        bool bIsRoad = inB && IsRoad(roadBX, roadBY);

        bool snappedOk = TrySnapToGroundStrict(candidate, signalYOffset, out var pos);

        bool insideBounds = true;
        Bounds gb = default;
        if (groundCollider != null)
        {
            gb = groundCollider.bounds;
            insideBounds =
                pos.x >= gb.min.x && pos.x <= gb.max.x &&
                pos.z >= gb.min.z && pos.z <= gb.max.z;
        }

        bool shouldSpawn =
            inCorner && inA && inB &&
            !cornerIsRoad && aIsRoad && bIsRoad &&
            snappedOk && insideBounds;

        if (debugSignals)
        {
            UnityEngine.Debug.DrawLine(candidate + Vector3.up * 0.2f, candidate + Vector3.up * 3f, shouldSpawn ? Color.green : Color.red, debugDrawSeconds);
            UnityEngine.Debug.DrawLine(pos + Vector3.up * 0.2f, pos + Vector3.up * 3f, shouldSpawn ? Color.green : Color.red, debugDrawSeconds);

            UnityEngine.Debug.Log(
                $"[SignalCorner] grid({x},{y}) sx/sy({sx},{sy}) " +
                $"corner({cornerX},{cornerY}) inCorner={inCorner} inA={inA} inB={inB} " +
                $"cornerIsRoad={cornerIsRoad} aIsRoad={aIsRoad} bIsRoad={bIsRoad} " +
                $"snappedOk={snappedOk} pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) " +
                (groundCollider != null ? $"groundBoundsX[{gb.min.x:F2},{gb.max.x:F2}] Z[{gb.min.z:F2},{gb.max.z:F2}] insideBounds={insideBounds}" : "groundCollider=NULL")
            );

            if (pauseOnBadSignal && snappedOk && groundCollider != null && !insideBounds)
            {
                UnityEngine.Debug.Break();
            }
        }

        if (!shouldSpawn) return;

        var tl = Instantiate(trafficLightPrefab, pos, Quaternion.identity, furnitureRoot);

        if (obstacleLayer >= 0) SetLayerRecursively(tl, obstacleLayer);

        EnsureTrafficLightLitVolume(tl);
    }

    // -----------------------------
    // Zebras (clusters)
    // -----------------------------

    bool HasTravelHorizontal(int x, int y) => IsRoad(x - 1, y) && IsRoad(x + 1, y);
    bool HasTravelVertical(int x, int y) => IsRoad(x, y - 1) && IsRoad(x, y + 1);

    void SpawnZebrasFromJunctionClusters(int w, int h)
    {
        bool[,] isJunctionCell = new bool[w, h];
        bool[,] visited = new bool[w, h];

        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                if (!IsRoad(x, y)) continue;
                if (!(HasTravelHorizontal(x, y) && HasTravelVertical(x, y))) continue;
                if (RoadNeighborCount(x, y) < 3) continue;

                isJunctionCell[x, y] = true;
            }
        }

        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                if (!isJunctionCell[x, y]) continue;
                if (visited[x, y]) continue;

                var cluster = FloodCluster(x, y, isJunctionCell, visited, w, h);
                if (cluster.Count == 0) continue;

                SpawnZebrasForCluster(cluster, w, h);
            }
        }
    }

    List<Vector2Int> FloodCluster(int sx, int sy, bool[,] isJunctionCell, bool[,] visited, int w, int h)
    {
        var result = new List<Vector2Int>(32);
        var q = new Queue<Vector2Int>();

        q.Enqueue(new Vector2Int(sx, sy));
        visited[sx, sy] = true;

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            result.Add(p);

            if (result.Count > maxJunctionClusterSize)
                break;

            TryEnqueue(p.x + 1, p.y);
            TryEnqueue(p.x - 1, p.y);
            TryEnqueue(p.x, p.y + 1);
            TryEnqueue(p.x, p.y - 1);
        }

        return result;

        void TryEnqueue(int x, int y)
        {
            if (x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1) return;
            if (visited[x, y]) return;
            if (!isJunctionCell[x, y]) return;

            visited[x, y] = true;
            q.Enqueue(new Vector2Int(x, y));
        }
    }

    void SpawnZebrasForCluster(List<Vector2Int> cluster, int w, int h)
    {
        var inCluster = new HashSet<int>();
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        for (int i = 0; i < cluster.Count; i++)
        {
            int x = cluster[i].x;
            int y = cluster[i].y;

            inCluster.Add(Pack(x, y, w));

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        bool forceBigRoad = ClusterTouchesBoulevard(cluster);

        var westApproaches = new List<Vector2Int>();
        var eastApproaches = new List<Vector2Int>();
        var southApproaches = new List<Vector2Int>();
        var northApproaches = new List<Vector2Int>();

        int wx = minX - 1;
        int ex = maxX + 1;
        int sy = minY - 1;
        int ny = maxY + 1;

        if (wx >= 1)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (!InBounds(wx, y)) continue;
                if (!IsRoad(wx, y)) continue;
                if (inCluster.Contains(Pack(wx, y, w))) continue;
                westApproaches.Add(new Vector2Int(wx, y));
            }
        }

        if (ex <= w - 2)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (!InBounds(ex, y)) continue;
                if (!IsRoad(ex, y)) continue;
                if (inCluster.Contains(Pack(ex, y, w))) continue;
                eastApproaches.Add(new Vector2Int(ex, y));
            }
        }

        if (sy >= 1)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!InBounds(x, sy)) continue;
                if (!IsRoad(x, sy)) continue;
                if (inCluster.Contains(Pack(x, sy, w))) continue;
                southApproaches.Add(new Vector2Int(x, sy));
            }
        }

        if (ny <= h - 2)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!InBounds(x, ny)) continue;
                if (!IsRoad(x, ny)) continue;
                if (inCluster.Contains(Pack(x, ny, w))) continue;
                northApproaches.Add(new Vector2Int(x, ny));
            }
        }

        SpawnZebrasOnSide(westApproaches, Vector2Int.right, horizontalTravel: true, forceBigRoad: forceBigRoad, minX, maxX, minY, maxY);
        SpawnZebrasOnSide(eastApproaches, Vector2Int.left, horizontalTravel: true, forceBigRoad: forceBigRoad, minX, maxX, minY, maxY);
        SpawnZebrasOnSide(southApproaches, Vector2Int.up, horizontalTravel: false, forceBigRoad: forceBigRoad, minX, maxX, minY, maxY);
        SpawnZebrasOnSide(northApproaches, Vector2Int.down, horizontalTravel: false, forceBigRoad: forceBigRoad, minX, maxX, minY, maxY);
    }

    void SpawnZebrasOnSide(
        List<Vector2Int> approaches,
        Vector2Int headingToCluster,
        bool horizontalTravel,
        bool forceBigRoad,
        int minX, int maxX, int minY, int maxY
    )
    {
        if (approaches == null || approaches.Count == 0) return;

        if (!forceBigRoad)
        {
            if (NextFloat() > smallRoadZebraChancePerSide) return;
        }
        else
        {
            if (NextFloat() > zebraChancePerSide) return;
        }

        for (int i = approaches.Count - 1; i >= 0; i--)
        {
            var a = approaches[i];
            if (horizontalTravel)
            {
                if (!LooksHorizontalLane(a.x, a.y)) approaches.RemoveAt(i);
            }
            else
            {
                if (!LooksVerticalLane(a.x, a.y)) approaches.RemoveAt(i);
            }
        }

        if (approaches.Count == 0) return;

        int sideKey =
            (minX * 73856093) ^ (maxX * 19349663) ^
            (minY * 83492791) ^ (maxY * 29765797) ^
            (headingToCluster.x * 91138211) ^
            (headingToCluster.y * 357239) ^
            (horizontalTravel ? 1231 : 4567);

        if (!zebraKeys.Add(sideKey)) return;

        float halfTile = roads.tileSize * 0.5f;
        float push = Mathf.Clamp(halfTile - zebraInsetFromJunction, 0f, halfTile);
        Vector3 dir = new Vector3(headingToCluster.x, 0f, headingToCluster.y);

        Quaternion baseRot = zebraCrossingPrefab != null ? zebraCrossingPrefab.transform.rotation : Quaternion.identity;
        if (rotateZebrasFlat) baseRot = Quaternion.Euler(zebraXRotation, 0f, 0f);

        float yaw = horizontalTravel ? 0f : 90f;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * baseRot;

        for (int i = 0; i < approaches.Count; i++)
        {
            int lx = approaches[i].x;
            int ly = approaches[i].y;

            Vector3 candidate = roads.GridToWorld(lx, ly);
            candidate -= dir * push;

            if (!TrySnapToGroundStrict(candidate, zebraYOffset, out var pos))
                continue;

            var z = Instantiate(zebraCrossingPrefab, pos, rot, furnitureRoot);

            if (forceZebrasWalkableLayer && walkableLayer >= 0)
                SetLayerRecursively(z, walkableLayer);
        }
    }

    int Pack(int x, int y, int width) => x + y * width;

    // -----------------------------
    // Boulevard / corridor tests
    // -----------------------------

    bool LooksHorizontalLane(int x, int y)
    {
        if (!IsRoad(x, y)) return false;
        return IsRoad(x - 1, y) || IsRoad(x + 1, y);
    }

    bool LooksVerticalLane(int x, int y)
    {
        if (!IsRoad(x, y)) return false;
        return IsRoad(x, y - 1) || IsRoad(x, y + 1);
    }

    bool IsBoulevardCell(int x, int y)
    {
        if (!IsRoad(x, y)) return false;

        bool horiz = LooksHorizontalLane(x, y);
        bool vert = LooksVerticalLane(x, y);

        if (horiz)
        {
            if (LooksHorizontalLane(x, y + 1)) return true;
            if (LooksHorizontalLane(x, y - 1)) return true;
        }

        if (vert)
        {
            if (LooksVerticalLane(x + 1, y)) return true;
            if (LooksVerticalLane(x - 1, y)) return true;
        }

        return false;
    }

    bool ClusterTouchesBoulevard(List<Vector2Int> cluster)
    {
        for (int i = 0; i < cluster.Count; i++)
        {
            int x = cluster[i].x;
            int y = cluster[i].y;

            if (IsBoulevardCell(x, y)) return true;
            if (IsBoulevardCell(x + 1, y)) return true;
            if (IsBoulevardCell(x - 1, y)) return true;
            if (IsBoulevardCell(x, y + 1)) return true;
            if (IsBoulevardCell(x, y - 1)) return true;
        }
        return false;
    }

    // -----------------------------
    // Junction + corridor tests
    // -----------------------------

    bool IsMultiLaneCorridorCell(int x, int y)
    {
        if (!IsRoad(x, y)) return false;

        if (IsStraightHorizontal(x, y))
        {
            if (IsStraightHorizontal(x, y + 1) || IsStraightHorizontal(x, y - 1)) return true;
            if (IsStraightHorizontal(x, y + 2) || IsStraightHorizontal(x, y - 2)) return true;
        }

        if (IsStraightVertical(x, y))
        {
            if (IsStraightVertical(x + 1, y) || IsStraightVertical(x - 1, y)) return true;
            if (IsStraightVertical(x + 2, y) || IsStraightVertical(x - 2, y)) return true;
        }

        return false;
    }

    bool IsStraight(int x, int y) => IsStraightHorizontal(x, y) || IsStraightVertical(x, y);

    bool IsStraightHorizontal(int x, int y)
    {
        return IsRoad(x - 1, y) && IsRoad(x + 1, y) && !IsRoad(x, y - 1) && !IsRoad(x, y + 1);
    }

    bool IsStraightVertical(int x, int y)
    {
        return IsRoad(x, y - 1) && IsRoad(x, y + 1) && !IsRoad(x - 1, y) && !IsRoad(x + 1, y);
    }

    int RoadNeighborCount(int x, int y)
    {
        int c = 0;
        if (IsRoad(x + 1, y)) c++;
        if (IsRoad(x - 1, y)) c++;
        if (IsRoad(x, y + 1)) c++;
        if (IsRoad(x, y - 1)) c++;
        return c;
    }

    bool FarFromOtherSignals(int x, int y)
    {
        int r = Mathf.Max(0, minTilesBetweenSignals);
        if (r == 0) return true;

        for (int ix = x - r; ix <= x + r; ix++)
        {
            for (int iy = y - r; iy <= y + r; iy++)
            {
                if (!InBounds(ix, iy)) continue;
                if (!hasSignal[ix, iy]) continue;

                int dx = ix - x;
                int dy = iy - y;
                if (dx * dx + dy * dy <= r * r) return false;
            }
        }
        return true;
    }

    // -----------------------------
    // Road side offset
    // -----------------------------

    Vector3 ChooseRoadSideOffset(int x, int y, float offset)
    {
        if (IsStraightHorizontal(x, y))
        {
            if (!IsRoad(x, y + 1)) return new Vector3(0f, 0f, +offset);
            if (!IsRoad(x, y - 1)) return new Vector3(0f, 0f, -offset);
            return Vector3.zero;
        }

        if (IsStraightVertical(x, y))
        {
            if (!IsRoad(x + 1, y)) return new Vector3(+offset, 0f, 0f);
            if (!IsRoad(x - 1, y)) return new Vector3(-offset, 0f, 0f);
            return Vector3.zero;
        }

        return Vector3.zero;
    }

    // -----------------------------
    // Ground snap (strict footprint)
    // -----------------------------

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

    float GetPrefabFootprintRadiusXZ(GameObject prefab)
    {
        if (prefab == null) return 0f;
        var r = prefab.GetComponentInChildren<Renderer>();
        if (r == null) return 0f;
        return Mathf.Max(r.bounds.extents.x, r.bounds.extents.z);
    }

    bool IsInsideGroundBoundsWithMargin(Vector3 p, float margin)
    {
        if (groundCollider == null) return true;

        var b = groundCollider.bounds;
        return p.x >= b.min.x + margin && p.x <= b.max.x - margin &&
               p.z >= b.min.z + margin && p.z <= b.max.z - margin;
    }

    // -----------------------------
    // Map helpers
    // -----------------------------

    bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < roads.Width && y < roads.Height;

    bool IsRoad(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        return roads.Map[x, y] == RoadPathGenerator.CellType.Road;
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

    float NextFloat()
    {
        return rng == null ? 0f : (float)rng.NextDouble();
    }
}
