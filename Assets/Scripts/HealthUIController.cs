using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthUIController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth playerHealth;
    public TextMeshProUGUI healthText;
    public FloatingTextSpawner floatingTextSpawner;

    [Header("Binding")]
    public float rebindInterval = 0.25f;

    [Header("Auto HUD")]
    public bool autoCreateHudIfMissing = true;

    [Header("Colors")]
    public Color damageColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color healColor = new Color(0.2f, 0.9f, 0.2f, 1f);

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
    }

    void UnbindPlayer()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnHealthDelta -= HandleHealthDelta;
    }

    void HandleHealthChanged(float current, float max)
    {
        if (healthText != null)
            healthText.text = "HP: " + FormatNumber(current) + "/" + FormatNumber(max);
    }

    void HandleHealthDelta(float delta)
    {
        if (floatingTextSpawner == null) return;
        if (Mathf.Abs(delta) < 0.0001f) return;

        if (delta < 0f)
            floatingTextSpawner.SpawnText(FormatNumber(delta), damageColor);
        else
            floatingTextSpawner.SpawnText("+" + FormatNumber(delta), healColor);
    }

    static string FormatNumber(float v)
    {
        float r = Mathf.Round(v);
        if (Mathf.Abs(v - r) < 0.0005f)
            return ((int)r).ToString(CultureInfo.InvariantCulture);

        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    void EnsureHudReferences()
    {
        if (healthText != null && floatingTextSpawner != null) return;

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
            hpRect.sizeDelta = new Vector2(320f, 120f);
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
                floatingRect.anchoredPosition = new Vector2(10f, -40f);
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
