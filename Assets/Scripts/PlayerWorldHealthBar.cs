using UnityEngine;

public class PlayerWorldHealthBar : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth playerHealth;

    [Header("Placement")]
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    public bool faceMainCamera = true;

    [Header("Size")]
    public float width = 1.2f;
    public float height = 0.14f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.75f);
    public Color fillColor = new Color(0.18f, 0.95f, 0.25f, 0.95f);

    Transform barRoot;
    Transform fillTransform;
    Renderer backgroundRenderer;
    Renderer fillRenderer;
    Material runtimeBackgroundMaterial;
    Material runtimeFillMaterial;
    Camera cam;

    void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        BuildBarIfNeeded();
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    void OnDestroy()
    {
        if (runtimeBackgroundMaterial != null)
            Destroy(runtimeBackgroundMaterial);

        if (runtimeFillMaterial != null)
            Destroy(runtimeFillMaterial);
    }

    void LateUpdate()
    {
        if (barRoot == null)
            BuildBarIfNeeded();

        if (barRoot == null) return;

        barRoot.position = transform.position + worldOffset;

        if (!faceMainCamera) return;

        if (cam == null)
            cam = Camera.main;
        if (cam == null) return;

        Vector3 toCamera = cam.transform.position - barRoot.position;
        if (toCamera.sqrMagnitude < 0.0001f) return;

        barRoot.forward = toCamera.normalized;
    }

    void HandleHealthChanged(int current, int max)
    {
        if (fillTransform == null) return;

        float pct = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
        fillTransform.localScale = new Vector3(Mathf.Max(0.0001f, pct), 1f, 1f);
        fillTransform.localPosition = new Vector3(-0.5f + (pct * 0.5f), 0f, -0.001f);
    }

    void BuildBarIfNeeded()
    {
        if (barRoot != null && fillTransform != null) return;

        Transform existing = transform.Find("WorldHealthBar");
        if (existing != null)
        {
            barRoot = existing;
            var bg = existing.Find("Background");
            var fill = existing.Find("Background/Fill");
            if (bg != null) backgroundRenderer = bg.GetComponent<Renderer>();
            if (fill != null)
            {
                fillTransform = fill;
                fillRenderer = fill.GetComponent<Renderer>();
            }

            return;
        }

        var root = new GameObject("WorldHealthBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = worldOffset;
        root.transform.localRotation = Quaternion.identity;

        var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Background";
        background.transform.SetParent(root.transform, false);
        background.transform.localPosition = Vector3.zero;
        background.transform.localScale = new Vector3(width, height, 1f);
        RemoveCollider(background);

        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "Fill";
        fill.transform.SetParent(background.transform, false);
        fill.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        fill.transform.localScale = Vector3.one;
        RemoveCollider(fill);

        backgroundRenderer = background.GetComponent<Renderer>();
        fillRenderer = fill.GetComponent<Renderer>();

        runtimeBackgroundMaterial = BuildUnlitMaterial(backgroundColor);
        runtimeFillMaterial = BuildUnlitMaterial(fillColor);

        if (backgroundRenderer != null)
            backgroundRenderer.sharedMaterial = runtimeBackgroundMaterial;

        if (fillRenderer != null)
            fillRenderer.sharedMaterial = runtimeFillMaterial;

        barRoot = root.transform;
        fillTransform = fill.transform;
    }

    Material BuildUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var material = new Material(shader);
        material.color = color;
        return material;
    }

    void RemoveCollider(GameObject target)
    {
        if (target == null) return;

        var collider = target.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }
}
