using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthUIController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth playerHealth;
    public TextMeshProUGUI healthText;
    public Image healthBarFill;
    public FloatingTextSpawner floatingTextSpawner;

    [Header("Binding")]
    public float rebindInterval = 0.25f;

    [Header("Auto HUD")]
    public bool autoCreateHudIfMissing = true;

    [Header("World Health Bar")]
    public bool autoAttachWorldHealthBar = true;

    [Header("Colors")]
    public Color damageColor = Color.red;
    public Color healColor = Color.green;
    public Color topLeftBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color topLeftBarFillColor = new Color(0.2f, 0.95f, 0.2f, 0.95f);

    float nextRebindTime;

    void Awake()
    {
        EnsureHudReferences();

        if (playerHealth == null)
            playerHealth = PlayerHealth.FindPlayerHealth();
    }

    void OnEnable()
    {
        BindPlayer();
    }

    void OnDisable()
    {
        UnbindPlayer();
    }

    void Update()
    {
        EnsureHudReferences();

        if (playerHealth != null) return;
        if (Time.unscaledTime < nextRebindTime) return;

        nextRebindTime = Time.unscaledTime + Mathf.Max(0.05f, rebindInterval);
        BindPlayer();
    }

    void BindPlayer()
    {
        if (playerHealth == null)
            playerHealth = PlayerHealth.FindPlayerHealth();

        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnHealthDelta -= HandleHealthDelta;

        playerHealth.OnHealthChanged += HandleHealthChanged;
        playerHealth.OnHealthDelta += HandleHealthDelta;

        HandleHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);

        if (autoAttachWorldHealthBar)
            EnsureWorldHealthBarAttached();
    }

    void UnbindPlayer()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnHealthDelta -= HandleHealthDelta;
    }

    void HandleHealthChanged(int current, int max)
    {
        if (healthText != null)
            healthText.text = $"HP: {current}/{max}";

        if (healthBarFill != null)
            healthBarFill.fillAmount = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
    }

    void HandleHealthDelta(int delta)
    {
        if (floatingTextSpawner == null) return;

        if (delta < 0)
            floatingTextSpawner.SpawnText($"{delta}", damageColor);
        else if (delta > 0)
            floatingTextSpawner.SpawnText($"+{delta}", healColor);
    }

    void EnsureWorldHealthBarAttached()
    {
        if (playerHealth == null) return;

        var worldBar = playerHealth.GetComponent<PlayerWorldHealthBar>();
        if (worldBar == null)
        {
            worldBar = playerHealth.gameObject.AddComponent<PlayerWorldHealthBar>();
            worldBar.playerHealth = playerHealth;
        }
    }

    void EnsureHudReferences()
    {
        if (healthText != null && healthBarFill != null && floatingTextSpawner != null) return;

        if (autoCreateHudIfMissing)
            CreateHudIfNeeded();
    }

    void CreateHudIfNeeded()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("HUD Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        Transform hpRoot = canvas.transform.Find("TopLeftHP");
        RectTransform hpRect;
        if (hpRoot == null)
        {
            var hpGo = new GameObject("TopLeftHP");
            hpRect = hpGo.AddComponent<RectTransform>();
            hpGo.transform.SetParent(canvas.transform, false);

            hpRect.anchorMin = new Vector2(0f, 1f);
            hpRect.anchorMax = new Vector2(0f, 1f);
            hpRect.pivot = new Vector2(0f, 1f);
            hpRect.anchoredPosition = new Vector2(24f, -20f);
            hpRect.sizeDelta = new Vector2(320f, 140f);
        }
        else
        {
            hpRect = hpRoot as RectTransform;
        }

        if (healthText == null)
        {
            Transform hpTextTransform = hpRect.Find("HealthText");
            if (hpTextTransform == null)
            {
                var textGo = new GameObject("HealthText");
                textGo.transform.SetParent(hpRect, false);

                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(0f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(280f, 56f);

                healthText = textGo.AddComponent<TextMeshProUGUI>();
                healthText.fontSize = 36f;
                healthText.alignment = TextAlignmentOptions.TopLeft;
                healthText.color = Color.white;
            }
            else
            {
                healthText = hpTextTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (healthBarFill == null)
        {
            Transform barRootTransform = hpRect.Find("HealthBarBackground");
            RectTransform barRect;
            Image barBg;

            if (barRootTransform == null)
            {
                var barGo = new GameObject("HealthBarBackground");
                barRect = barGo.AddComponent<RectTransform>();
                barGo.transform.SetParent(hpRect, false);

                barRect.anchorMin = new Vector2(0f, 1f);
                barRect.anchorMax = new Vector2(0f, 1f);
                barRect.pivot = new Vector2(0f, 1f);
                barRect.anchoredPosition = new Vector2(0f, -56f);
                barRect.sizeDelta = new Vector2(280f, 24f);

                barBg = barGo.AddComponent<Image>();
                barBg.color = topLeftBarBackgroundColor;
            }
            else
            {
                barRect = barRootTransform as RectTransform;
                barBg = barRootTransform.GetComponent<Image>();
                if (barBg != null)
                    barBg.color = topLeftBarBackgroundColor;
            }

            Transform fillTransform = barRect != null ? barRect.Find("HealthBarFill") : null;
            if (fillTransform == null)
            {
                var fillGo = new GameObject("HealthBarFill");
                fillGo.transform.SetParent(barRect, false);

                var fillRect = fillGo.AddComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.offsetMin = new Vector2(2f, 2f);
                fillRect.offsetMax = new Vector2(-2f, -2f);

                healthBarFill = fillGo.AddComponent<Image>();
            }
            else
            {
                healthBarFill = fillTransform.GetComponent<Image>();
            }

            if (healthBarFill != null)
            {
                healthBarFill.color = topLeftBarFillColor;
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
                healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                healthBarFill.fillAmount = 1f;
            }
        }

        if (floatingTextSpawner == null)
        {
            Transform floatingRoot = hpRect.Find("FloatingDamageRoot");
            RectTransform floatingRect;

            if (floatingRoot == null)
            {
                var floatingGo = new GameObject("FloatingDamageRoot");
                floatingRect = floatingGo.AddComponent<RectTransform>();
                floatingGo.transform.SetParent(hpRect, false);

                floatingRect.anchorMin = new Vector2(0f, 1f);
                floatingRect.anchorMax = new Vector2(0f, 1f);
                floatingRect.pivot = new Vector2(0f, 1f);
                floatingRect.anchoredPosition = new Vector2(10f, -88f);
                floatingRect.sizeDelta = new Vector2(260f, 100f);
            }
            else
            {
                floatingRect = floatingRoot as RectTransform;
            }

            floatingTextSpawner = floatingRect.GetComponent<FloatingTextSpawner>();
            if (floatingTextSpawner == null)
                floatingTextSpawner = floatingRect.gameObject.AddComponent<FloatingTextSpawner>();

            floatingTextSpawner.spawnRoot = floatingRect;
        }
    }
}
