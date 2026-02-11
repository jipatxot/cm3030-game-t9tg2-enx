using UnityEngine;
using UnityEngine.UI;

public class PlayerWorldHealthBar : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth playerHealth;

    [Header("Placement")]
    public Vector3 worldOffset = new Vector3(0f, 2.3f, 0f);
    public bool faceMainCamera = true;

    [Header("Size")]
    public Vector2 barSize = new Vector2(140f, 18f);
    public float worldScale = 0.01f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color fillColor = new Color(0.2f, 0.95f, 0.2f, 0.95f);

    Transform barRoot;
    Image fillImage;
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

        Vector3 toCamera = barRoot.position - cam.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f) return;

        barRoot.forward = toCamera.normalized;
    }

    void HandleHealthChanged(int current, int max)
    {
        if (fillImage == null) return;

        float pct = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
        fillImage.fillAmount = pct;
    }

    void BuildBarIfNeeded()
    {
        if (barRoot != null && fillImage != null) return;

        Transform existing = transform.Find("WorldHealthBar");
        if (existing != null)
        {
            barRoot = existing;
            var fill = existing.Find("Background/Fill");
            if (fill != null)
                fillImage = fill.GetComponent<Image>();
            return;
        }

        var root = new GameObject("WorldHealthBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = worldOffset;
        root.transform.localRotation = Quaternion.identity;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 999;
        root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 30f;

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = barSize;
        rect.localScale = Vector3.one * Mathf.Max(0.0001f, worldScale);

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(root.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bg = bgGo.AddComponent<Image>();
        bg.color = backgroundColor;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(0.02f, 0.02f);
        fillRect.offsetMax = new Vector2(-0.02f, -0.02f);

        fillImage = fillGo.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        barRoot = root.transform;
    }
}
