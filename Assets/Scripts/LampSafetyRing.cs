using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LampSafetyRing : MonoBehaviour
{
    [Header("Sources (auto if null)")]
    public LampSafeZone safeZone;
    public LightPowerDecay power;

    [Header("Ring Shape")]
    [Min(3)] public int segments = 64;
    [Min(0.001f)] public float yOffset = 0.05f;

    [Header("Radius Mapping")]
    public bool shrinkWithPower = true;
    [Range(0f, 1f)] public float minRadiusPercentAtZero = 0f;

    [Header("Visibility Gate")]
    [Range(0f, 1f)] public float hideBelowPower01 = 0.01f;
    public bool alsoHideBelowSafeThreshold = true;

    [Header("Colors")]
    public Color highColor = new Color(0.15f, 1f, 0.15f, 0.35f);
    public Color midColor = new Color(1f, 0.85f, 0.1f, 0.30f);
    public Color lowColor = new Color(1f, 0.2f, 0.2f, 0.30f);
    [Range(0f, 1f)] public float highFrom01 = 0.70f;
    [Range(0f, 1f)] public float midFrom01 = 0.30f;

    [Header("Line")]
    [Min(0.001f)] public float lineWidth = 0.05f;

    LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();

        if (safeZone == null) safeZone = GetComponentInParent<LampSafeZone>();
        if (power == null) power = GetComponentInParent<LightPowerDecay>();

        SetupLineRenderer();
        ForceRingTransform();
        RefreshRing();
    }

    void OnValidate()
    {
        lr = GetComponent<LineRenderer>();
        if (safeZone == null) safeZone = GetComponentInParent<LampSafeZone>();
        if (power == null) power = GetComponentInParent<LightPowerDecay>();

        if (lr != null)
        {
            SetupLineRenderer();
            ForceRingTransform();
            RefreshRing();
        }
    }

    void Update()
    {
        if (safeZone == null) safeZone = GetComponentInParent<LampSafeZone>();
        if (power == null) power = GetComponentInParent<LightPowerDecay>();
        if (lr == null) lr = GetComponent<LineRenderer>();

        if (lr == null || safeZone == null || power == null) return;

        ForceRingTransform();
        RefreshRing();
    }

    void SetupLineRenderer()
    {
        if (lr == null) return;

        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = Mathf.Max(3, segments);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Shader s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Unlit/Color");

        if (s != null)
        {
            var mat = new Material(s);
            mat.name = "LampSafetyRing_Mat_Runtime";
            lr.material = mat;
        }
    }

    void ForceRingTransform()
    {
        transform.localPosition = new Vector3(0f, yOffset, 0f);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    void RefreshRing()
    {
        float p01 = Mathf.Clamp01(power.NormalizedPower01);

        if (p01 < hideBelowPower01)
        {
            lr.enabled = false;
            return;
        }

        if (alsoHideBelowSafeThreshold && p01 < safeZone.minPower01ForSafe)
        {
            lr.enabled = false;
            return;
        }

        lr.enabled = true;

        float baseRadius = Mathf.Max(0.001f, safeZone.safeRadius);
        float radius01 = shrinkWithPower ? Mathf.Lerp(minRadiusPercentAtZero, 1f, p01) : 1f;
        float targetWorldRadius = baseRadius * radius01;

        BuildCircleLocal(targetWorldRadius, targetWorldRadius);

        Color c = PickColor(p01);
        c.a *= Mathf.Lerp(0.4f, 1f, p01);

        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = c;
        lr.endColor = c;
    }

    Color PickColor(float p01)
    {
        if (p01 >= highFrom01) return highColor;
        if (p01 >= midFrom01) return midColor;
        return lowColor;
    }

    void BuildCircleLocal(float radiusX, float radiusZ)
    {
        int count = Mathf.Max(3, segments);
        lr.positionCount = count;

        float step = Mathf.PI * 2f / count;

        for (int i = 0; i < count; i++)
        {
            float a = i * step;
            float x = Mathf.Cos(a) * radiusX;
            float z = Mathf.Sin(a) * radiusZ;
            lr.SetPosition(i, new Vector3(x, 0f, z));
        }
    }
}