using UnityEngine;

public class ProceduralBuildingVisual : MonoBehaviour
{
    [Header("Prefab parts")]
    public Transform roofCapPrefab;
    public Transform doorPrefab;
    public Material doorMaterial;

    [Header("Window limits for performance")]
    public bool buildWindows = true;
    public int maxColsPerSide = 8;
    public int maxRowsPerSide = 10;
    [Range(0f, 1f)] public float windowSkipChance = 0.0f;

    [Header("Placement tweaks")]
    public float roofCapLift = 0.03f;
    public float roofCapOutset = 0.04f;

    [Tooltip("If your prefab window quad is rotated 180°, enable this.")]
    public bool flipWindowFacing = true;

    [Tooltip("If your prefab door quad is rotated 180°, enable this.")]
    public bool flipDoorFacing = true;

    [Header("Global size multiplier")]
    [Tooltip("2 means windows and door become 2x larger, so fewer windows fit.")]
    public float openingsScale = 1f;

    [Header("Refs")]
    public Transform body;
    public Transform windowsRoot;

    [Header("Facade look")]
    public Material facadeMaterial;                 // URP Lit or Standard
    public Vector2 facadeGrayRange = new Vector2(0.10f, 0.28f);
    [Range(0f, 0.5f)] public float facadeMetallic = 0f;
    [Range(0f, 1f)] public float facadeSmoothness = 0.2f;

    [Header("Roof cap")]
    public bool addRoofCap = true;
    public float roofCapHeight = 0.08f;
    public float roofCapInset = 0.04f;

    [Header("Door")]
    public bool addDoor = true;
    public Vector2 doorSize = new Vector2(0.28f, 0.42f);
    public float doorInset = 0.012f;
    public Color doorColor = new Color(0.08f, 0.08f, 0.08f, 1f);

    [Header("Window look")]
    public Material windowMaterial;                 // emissive capable
    public Vector2 windowSize = new Vector2(0.18f, 0.18f);
    public Vector2 windowGap = new Vector2(0.10f, 0.14f);
    [Range(0f, 1f)] public float litChance = 0.25f;

    [Header("Per building random")]
    public bool randomizeLitChance = true;
    public Vector2 litChanceRange = new Vector2(0.12f, 0.45f);

    [Header("Window emission")]
    public float litEmissionMin = 1.4f;
    public float litEmissionMax = 3.2f;

    // cache so we do not re-create every rebuild
    Transform roofCap;
    Transform doorQuad;

    public void Rebuild(float width, float depth, float height, int floors)
    {
        if (body == null || windowsRoot == null) return;

        ClearChildren(windowsRoot);

        // Scale body
        body.localScale = new Vector3(width, height, depth);

        // Bottom-anchor visuals so nothing goes underground
        body.localPosition = new Vector3(0f, height * 0.5f, 0f);

        // IMPORTANT FIX:
        // windows are computed in 0..height space already, so keep the root at 0
        windowsRoot.localPosition = Vector3.zero;

        ApplyFacade(width, depth, height);
        BuildRoofCap(width, depth, height);
        BuildDoor(width, depth, height);

        float chance = litChance;
        if (randomizeLitChance)
            chance = Random.Range(litChanceRange.x, litChanceRange.y);

        BuildWindowsOnSide(Vector3.forward, width, height, depth, floors, chance);
        BuildWindowsOnSide(Vector3.back, width, height, depth, floors, chance);
        BuildWindowsOnSide(Vector3.right, depth, height, width, floors, chance);
        BuildWindowsOnSide(Vector3.left, depth, height, width, floors, chance);
    }



    void ApplyFacade(float width, float depth, float height)
    {
        var mr = body.GetComponent<MeshRenderer>();
        if (mr == null) return;

        if (facadeMaterial != null)
            mr.sharedMaterial = facadeMaterial;

        // Random dark grey per building
        float g = Random.Range(facadeGrayRange.x, facadeGrayRange.y);
        Color baseCol = new Color(g, g, g, 1f);

        var mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);

        // Works for URP Lit and Standard in most setups
        mpb.SetColor("_BaseColor", baseCol);
        mpb.SetColor("_Color", baseCol);

        mpb.SetFloat("_Metallic", facadeMetallic);
        mpb.SetFloat("_Smoothness", facadeSmoothness);
        mpb.SetFloat("_Glossiness", facadeSmoothness);

        mr.SetPropertyBlock(mpb);
    }

    void BuildRoofCap(float width, float depth, float height)
    {
        if (!addRoofCap)
        {
            if (roofCap != null) roofCap.gameObject.SetActive(false);
            return;
        }

        if (roofCap == null)
        {
            // IMPORTANT FIX:
            // If roofCapPrefab is a child on the prefab, DO NOT Instantiate it.
            // Just use it.
            if (roofCapPrefab != null)
            {
                roofCap = roofCapPrefab;
                roofCap.SetParent(transform, false);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "RoofCap";
                roofCap = go.transform;
                roofCap.SetParent(transform, false);
                Destroy(go.GetComponent<Collider>());

                var mr = go.GetComponent<MeshRenderer>();
                if (facadeMaterial != null) mr.sharedMaterial = facadeMaterial;
            }
        }

        roofCap.gameObject.SetActive(true);

        float capW = Mathf.Max(0.02f, width - roofCapInset * 2f);
        float capD = Mathf.Max(0.02f, depth - roofCapInset * 2f);

        roofCap.localScale = new Vector3(capW, roofCapHeight, capD);

        roofCap.localPosition = new Vector3(
            0f,
            height + roofCapLift + roofCapOutset + roofCapHeight * 0.5f,
            0f
        );

        var mr2 = roofCap.GetComponent<MeshRenderer>();
        if (mr2 != null)
        {
            float g = Random.Range(facadeGrayRange.x, facadeGrayRange.y) * 0.85f;
            Color capCol = new Color(g, g, g, 1f);

            var mpb = new MaterialPropertyBlock();
            mr2.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", capCol);
            mpb.SetColor("_Color", capCol);
            mr2.SetPropertyBlock(mpb);
        }
    }

    void BuildDoor(float width, float depth, float height)
    {
        if (!addDoor)
        {
            if (doorQuad != null) doorQuad.gameObject.SetActive(false);
            return;
        }

        if (doorQuad == null)
        {
            if (doorPrefab != null)
            {
                doorQuad = Instantiate(doorPrefab, transform, false);
                doorQuad.name = "Door";
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Door";
                doorQuad = go.transform;
                doorQuad.SetParent(transform, false);
                Destroy(go.GetComponent<Collider>());

                var mr = go.GetComponent<MeshRenderer>();
                if (facadeMaterial != null) mr.sharedMaterial = facadeMaterial;
            }
        }

        doorQuad.gameObject.SetActive(true);

        float s = Mathf.Max(0.01f, openingsScale);
        Vector2 ds = doorSize * s;

        float z = (depth * 0.5f) + doorInset;

        Vector3 faceNormal = flipDoorFacing ? -Vector3.forward : Vector3.forward;
        doorQuad.localRotation = Quaternion.LookRotation(faceNormal, Vector3.up);
        doorQuad.localScale = new Vector3(ds.x, ds.y, 1f);

        float x = Random.Range(-width * 0.18f, width * 0.18f);

        // Bottom-anchored: ground is y=0
        float y = (ds.y * 0.5f) + 0.02f;

        doorQuad.localPosition = new Vector3(x, y, z);

        var mr2 = doorQuad.GetComponent<MeshRenderer>();
        if (mr2 != null)
        {
            if (doorMaterial != null) mr2.sharedMaterial = doorMaterial;

            var mpb = new MaterialPropertyBlock();
            mr2.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", doorColor);
            mpb.SetColor("_Color", doorColor);
            mr2.SetPropertyBlock(mpb);
        }
    }


    void BuildWindowsOnSide(Vector3 normal, float sideLen, float height, float otherLen, int floors, float chance)
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

        // Performance caps
        cols = Mathf.Min(cols, Mathf.Max(1, maxColsPerSide));
        rows = Mathf.Min(rows, Mathf.Max(1, maxRowsPerSide));

        float totalW = cols * ws.x + (cols - 1) * wg.x;
        float totalH = rows * ws.y + (rows - 1) * wg.y;

        // Center the grid so you don’t get empty strips on one side
        float xStart = leftX + (usableX - totalW) * 0.5f + ws.x * 0.5f;
        float yStart = bottomY + (usableY - totalH) * 0.5f + ws.y * 0.5f;

        float faceOffset = otherLen * 0.5f + 0.006f;

        Vector3 faceNormal = flipWindowFacing ? -normal : normal;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (windowSkipChance > 0f && Random.value < windowSkipChance) continue;

                bool lit = Random.value < chance;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = lit ? "Window_Lit" : "Window_Off";
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
}