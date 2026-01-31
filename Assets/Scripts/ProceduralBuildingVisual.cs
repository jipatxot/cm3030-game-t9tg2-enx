using UnityEngine;

public class ProceduralBuildingVisual : MonoBehaviour
{
    [Header("Roof prefabs (assets)")]
    public Transform roofCapPrefab;
    public Transform pitchedRoofPrefab;

    [Header("Door")]
    public Transform doorPrefab;
    public Material doorMaterial;

    [Header("Refs")]
    public Transform body;
    public Transform windowsRoot;

    [Header("Roof sizing")]
    public bool addRoof = true;

    // Flat roof sizing
    public float flatRoofLift = 0.002f;
    public float flatRoofOutset = 0.00f;
    public float flatRoofInset = 0.1f;
    public float flatRoofHeight = 0.08f;

    // Pitched roof sizing
    public float pitchedRoofLift = 0.002f;
    public float pitchedRoofOutset = 0.1f;
    public float pitchedRoofInset = 0.00f;

    [Tooltip("Extra X/Z scale for pitched roofs after inset/outset is applied.")]
    public float pitchedRoofExtraScaleXZ = 1.00f;

    [Header("Pitched roof rule")]
    public bool usePitchedRoofForShortBuildings = true;

    [Tooltip("Use pitched roof when floors <= this.")]
    public int pitchedRoofMaxFloors = 2;

    [Tooltip("Tile size in world units.")]
    public float worldUnitsPerTile = 1f;

    [Tooltip("Only allow pitched roof on EXACT footprints. Example: (2,2) only.")]
    public Vector2Int[] pitchedRoofAllowedFootprints = new Vector2Int[]
    {
        new Vector2Int(2,2)
    };

    [Tooltip("How close width/depth must be to N tiles to count as that footprint. Smaller = stricter.")]
    public float footprintTolerance = 0.08f;

    [Header("Door placement")]
    public float doorSideRandomFrac = 0.18f;

    [Header("Door look")]
    public bool addDoor = true;
    public Vector2 doorSize = new Vector2(0.28f, 0.42f);
    public float doorInset = 0.012f;
    public Color doorColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    public bool flipDoorFacing = true;

    [Header("Facade look")]
    public Material facadeMaterial;
    public Vector2 facadeGrayRange = new Vector2(0.10f, 0.28f);
    [Range(0f, 0.5f)] public float facadeMetallic = 0f;
    [Range(0f, 1f)] public float facadeSmoothness = 0.2f;

    [Header("Windows")]
    public bool buildWindows = true;
    public int maxColsPerSide = 8;
    public int maxRowsPerSide = 10;
    [Range(0f, 1f)] public float windowSkipChance = 0.0f;
    public Material windowMaterial;
    public Vector2 windowSize = new Vector2(0.18f, 0.18f);
    public Vector2 windowGap = new Vector2(0.10f, 0.14f);
    [Range(0f, 1f)] public float litChance = 0.25f;
    public bool randomizeLitChance = true;
    public Vector2 litChanceRange = new Vector2(0.12f, 0.45f);
    public float litEmissionMin = 1.4f;
    public float litEmissionMax = 3.2f;
    public bool flipWindowFacing = true;
    public float openingsScale = 1f;

    Transform roofFlatInst;
    Transform roofPitchedInst;
    Transform doorQuad;

    public void Rebuild(float width, float depth, float height, int floors)
    {
        if (body == null || windowsRoot == null) return;

        ClearChildren(windowsRoot);

        body.localScale = new Vector3(width, height, depth);
        body.localPosition = new Vector3(0f, height * 0.5f, 0f);
        windowsRoot.localPosition = Vector3.zero;

        ApplyFacade();

        EnsureRoofInstances();

        bool usePitched = ShouldUsePitchedRoof(width, depth, floors);

        if (!addRoof)
        {
            SetActive(roofFlatInst, false);
            SetActive(roofPitchedInst, false);
        }
        else if (usePitched)
        {
            SetActive(roofFlatInst, false);
            FitAndSnapRoof(
                roofPitchedInst, width, depth, height,
                keepYScale: true,
                inset: pitchedRoofInset,
                outset: pitchedRoofOutset,
                lift: pitchedRoofLift,
                extraXZ: pitchedRoofExtraScaleXZ
            );
        }
        else
        {
            SetActive(roofPitchedInst, false);
            FitAndSnapRoof(
                roofFlatInst, width, depth, height,
                keepYScale: false,
                inset: flatRoofInset,
                outset: flatRoofOutset,
                lift: flatRoofLift,
                extraXZ: 1f
            );
        }

        BuildDoor(width, depth);

        float chance = litChance;
        if (randomizeLitChance)
            chance = Random.Range(litChanceRange.x, litChanceRange.y);

        BuildWindowsOnSide(Vector3.forward, width, height, depth, chance);
        BuildWindowsOnSide(Vector3.back, width, height, depth, chance);
        BuildWindowsOnSide(Vector3.right, depth, height, width, chance);
        BuildWindowsOnSide(Vector3.left, depth, height, width, chance);
    }

    void EnsureRoofInstances()
    {
        if (roofFlatInst == null && roofCapPrefab != null)
        {
            roofFlatInst = Instantiate(roofCapPrefab, transform, false);
            roofFlatInst.name = "RoofCap";
        }

        if (roofPitchedInst == null && pitchedRoofPrefab != null)
        {
            roofPitchedInst = Instantiate(pitchedRoofPrefab, transform, false);
            roofPitchedInst.name = "PitchedRoof";
        }
    }

    bool ShouldUsePitchedRoof(float width, float depth, int floors)
    {
        if (!usePitchedRoofForShortBuildings) return false;
        if (floors > pitchedRoofMaxFloors) return false;
        if (roofPitchedInst == null && pitchedRoofPrefab == null) return false;

        Vector2Int fp = InferFootprintStrict(width, depth);

        if (pitchedRoofAllowedFootprints == null || pitchedRoofAllowedFootprints.Length == 0)
            return true;

        for (int i = 0; i < pitchedRoofAllowedFootprints.Length; i++)
            if (pitchedRoofAllowedFootprints[i] == fp)
                return true;

        return false;
    }

    Vector2Int InferFootprintStrict(float width, float depth)
    {
        float tile = Mathf.Max(0.0001f, worldUnitsPerTile);

        float tx = width / tile;
        float tz = depth / tile;

        int nx = Mathf.Max(1, Mathf.RoundToInt(tx));
        int nz = Mathf.Max(1, Mathf.RoundToInt(tz));

        float dx = Mathf.Abs(width - nx * tile);
        float dz = Mathf.Abs(depth - nz * tile);

        if (dx > footprintTolerance) nx = Mathf.Max(1, Mathf.FloorToInt((width + 0.0001f) / tile));
        if (dz > footprintTolerance) nz = Mathf.Max(1, Mathf.FloorToInt((depth + 0.0001f) / tile));

        return new Vector2Int(nx, nz);
    }

    void FitAndSnapRoof(
        Transform roof,
        float width,
        float depth,
        float buildingTopY,
        bool keepYScale,
        float inset,
        float outset,
        float lift,
        float extraXZ
    )
    {
        if (roof == null) return;

        SetActive(roof, true);

        float targetW = Mathf.Max(0.02f, (width - inset * 2f) + outset * 2f);
        float targetD = Mathf.Max(0.02f, (depth - inset * 2f) + outset * 2f);

        targetW *= Mathf.Max(0.01f, extraXZ);
        targetD *= Mathf.Max(0.01f, extraXZ);

        Vector2 sizeXZ = GetWorldSizeXZ(roof);

        Vector3 s = roof.localScale;

        if (sizeXZ.x > 0.0001f) s.x *= (targetW / sizeXZ.x);
        if (sizeXZ.y > 0.0001f) s.z *= (targetD / sizeXZ.y);

        if (!keepYScale) s.y = flatRoofHeight;

        roof.localScale = s;

        float bottomLocalY = GetLocalBottomYFromChildRenderers(roof);
        float targetBottomY = buildingTopY + lift;
        float deltaY = targetBottomY - bottomLocalY;

        Vector3 p = roof.localPosition;
        roof.localPosition = new Vector3(0f, p.y + deltaY, 0f);
        roof.localRotation = Quaternion.identity;
    }

    Vector2 GetWorldSizeXZ(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return Vector2.one;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return new Vector2(Mathf.Max(0.0001f, b.size.x), Mathf.Max(0.0001f, b.size.z));
    }

    float GetLocalBottomYFromChildRenderers(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return t.localPosition.y;

        float minLocalY = float.PositiveInfinity;

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds b = renderers[i].bounds;
            Vector3 min = b.min;
            Vector3 max = b.max;

            for (int xi = 0; xi < 2; xi++)
                for (int yi = 0; yi < 2; yi++)
                    for (int zi = 0; zi < 2; zi++)
                    {
                        Vector3 w = new Vector3(
                            xi == 0 ? min.x : max.x,
                            yi == 0 ? min.y : max.y,
                            zi == 0 ? min.z : max.z
                        );

                        Vector3 local = transform.InverseTransformPoint(w);
                        if (local.y < minLocalY) minLocalY = local.y;
                    }
        }

        return minLocalY;
    }

    Camera GetDoorCamera()
    {
        if (Camera.main != null) return Camera.main;

        var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
            if (cams[i] != null && cams[i].enabled)
                return cams[i];

        return null;
    }

    Vector3 ChooseDoorFaceFromCamera()
    {
        Camera cam = GetDoorCamera();
        if (cam == null) return Vector3.forward;

        Vector3 toCam = (cam.transform.position - transform.position).normalized;

        Vector3[] faces = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        float best1 = -999f; int idx1 = 0;
        float best2 = -999f; int idx2 = 1;

        for (int i = 0; i < faces.Length; i++)
        {
            float s = Vector3.Dot(faces[i], toCam);
            if (s > best1) { best2 = best1; idx2 = idx1; best1 = s; idx1 = i; }
            else if (s > best2) { best2 = s; idx2 = i; }
        }

        if (best1 <= 0f && best2 <= 0f)
            return Random.value < 0.5f ? Vector3.forward : Vector3.right;

        return Random.value < 0.5f ? faces[idx1] : faces[idx2];
    }

    void BuildDoor(float width, float depth)
    {
        if (!addDoor) { SetActive(doorQuad, false); return; }

        if (doorQuad == null)
        {
            if (doorPrefab != null) doorQuad = Instantiate(doorPrefab, transform, false);
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                doorQuad = go.transform;
                doorQuad.SetParent(transform, false);
                Destroy(go.GetComponent<Collider>());
            }
            doorQuad.name = "Door";
        }

        SetActive(doorQuad, true);

        float s = Mathf.Max(0.01f, openingsScale);
        Vector2 ds = doorSize * s;

        Vector3 face = ChooseDoorFaceFromCamera();
        float y = (ds.y * 0.5f) + 0.02f;

        if (face == Vector3.forward || face == Vector3.back)
        {
            float z = (depth * 0.5f) + doorInset;
            z *= Mathf.Sign(face.z);
            float x = Random.Range(-width * doorSideRandomFrac, width * doorSideRandomFrac);

            Vector3 faceNormal = flipDoorFacing ? -face : face;
            doorQuad.localRotation = Quaternion.LookRotation(faceNormal, Vector3.up);
            doorQuad.localScale = new Vector3(ds.x, ds.y, 1f);
            doorQuad.localPosition = new Vector3(x, y, z);
        }
        else
        {
            float x = (width * 0.5f) + doorInset;
            x *= Mathf.Sign(face.x);
            float z = Random.Range(-depth * doorSideRandomFrac, depth * doorSideRandomFrac);

            Vector3 faceNormal = flipDoorFacing ? -face : face;
            doorQuad.localRotation = Quaternion.LookRotation(faceNormal, Vector3.up);
            doorQuad.localScale = new Vector3(ds.x, ds.y, 1f);
            doorQuad.localPosition = new Vector3(x, y, z);
        }

        var mr = doorQuad.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (doorMaterial != null) mr.sharedMaterial = doorMaterial;

            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", doorColor);
            mpb.SetColor("_Color", doorColor);
            mr.SetPropertyBlock(mpb);
        }
    }

    void ApplyFacade()
    {
        var mr = body != null ? body.GetComponent<MeshRenderer>() : null;
        if (mr == null) return;

        if (facadeMaterial != null) mr.sharedMaterial = facadeMaterial;

        float g = Random.Range(facadeGrayRange.x, facadeGrayRange.y);
        Color baseCol = new Color(g, g, g, 1f);

        var mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", baseCol);
        mpb.SetColor("_Color", baseCol);
        mpb.SetFloat("_Metallic", facadeMetallic);
        mpb.SetFloat("_Smoothness", facadeSmoothness);
        mpb.SetFloat("_Glossiness", facadeSmoothness);
        mr.SetPropertyBlock(mpb);
    }

    void BuildWindowsOnSide(Vector3 normal, float sideLen, float height, float otherLen, float chance)
    {
        if (!buildWindows) return;
        if (windowsRoot == null) return;
        if (windowMaterial == null) return;

        float s = Mathf.Max(0.01f, openingsScale);
        Vector2 ws = windowSize * s;
        Vector2 wg = windowGap * s;

        float borderX = 0.25f;
        float borderY = 0.35f;

        float leftX = -sideLen * 0.5f + borderX;
        float rightX = sideLen * 0.5f - borderX;
        float usableX = Mathf.Max(0f, rightX - leftX);

        float bottomY = borderY;
        float topY = height - borderY;
        float usableY = Mathf.Max(0f, topY - bottomY);

        float stepX = ws.x + wg.x;
        float stepY = ws.y + wg.y;

        int cols = Mathf.Max(1, Mathf.FloorToInt((usableX + wg.x) / stepX));
        int rows = Mathf.Max(1, Mathf.FloorToInt((usableY + wg.y) / stepY));

        cols = Mathf.Min(cols, Mathf.Max(1, maxColsPerSide));
        rows = Mathf.Min(rows, Mathf.Max(1, maxRowsPerSide));

        float totalW = cols * ws.x + (cols - 1) * wg.x;
        float totalH = rows * ws.y + (rows - 1) * wg.y;

        float xStart = leftX + (usableX - totalW) * 0.5f + ws.x * 0.5f;
        float yStart = bottomY + (usableY - totalH) * 0.5f + ws.y * 0.5f;

        float faceOffset = otherLen * 0.5f + 0.006f;
        Vector3 faceNormal = flipWindowFacing ? -normal : normal;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (windowSkipChance > 0f && Random.value < windowSkipChance) continue;

                bool lit = Random.value < chance;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.transform.SetParent(windowsRoot, false);

                float x = xStart + c * stepX;
                float y = yStart + r * stepY;

                Vector3 localPos;
                if (normal == Vector3.forward || normal == Vector3.back)
                    localPos = new Vector3(x, y, normal.z * faceOffset);
                else
                    localPos = new Vector3(normal.x * faceOffset, y, x);

                go.transform.localPosition = localPos;
                go.transform.localRotation = Quaternion.LookRotation(faceNormal, Vector3.up);
                go.transform.localScale = new Vector3(ws.x, ws.y, 1f);

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = windowMaterial;

                var mpb = new MaterialPropertyBlock();
                mr.GetPropertyBlock(mpb);

                if (lit)
                {
                    float e = Random.Range(litEmissionMin, litEmissionMax);
                    mpb.SetColor("_EmissionColor", Color.white * e);
                    mr.SetPropertyBlock(mpb);
                    mr.sharedMaterial.EnableKeyword("_EMISSION");
                }
                else
                {
                    mpb.SetColor("_EmissionColor", Color.black);
                    mr.SetPropertyBlock(mpb);
                }

                Destroy(go.GetComponent<Collider>());
            }
    }

    void SetActive(Transform t, bool active)
    {
        if (t == null) return;
        if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(parent.GetChild(i).gameObject);
            else Destroy(parent.GetChild(i).gameObject);
#else
            Destroy(parent.GetChild(i).gameObject);
#endif
        }
    }
}
