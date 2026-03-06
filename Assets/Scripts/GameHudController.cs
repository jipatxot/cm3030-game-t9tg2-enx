using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameHudController : MonoBehaviour
{
    [Header("Refs (leave blank, auto-finds)")]
    public PlayerHealth playerHealth;
    public PowerDecayManager powerManager;
    public PowerRepairInteraction repairInteraction;

    [Header("Rebind")]
    public float rebindInterval = 0.25f;
    float nextRebindTime;

    [Header("Health bar")]
    public Image healthFill;
    public Color healthGreen = new Color(0.2f, 0.9f, 0.2f, 1f);
    public Color healthOrange = new Color(0.95f, 0.6f, 0.15f, 1f);
    public Color healthRed = new Color(0.9f, 0.2f, 0.2f, 1f);
    [Range(0f, 1f)] public float orangeThreshold = 0.70f;
    [Range(0f, 1f)] public float redThreshold = 0.30f;

    [Header("Optional top-left HP text (leave blank to disable)")]
    public TMP_Text hpLabelText;
    public TMP_Text hpValueText;
    public bool hpShowAsFraction = true;

    [Header("Floating damage (optional, no extra script)")]
    public RectTransform floatingSpawnRoot;
    public TextMeshProUGUI floatingTextPrefab;
    public float floatingFloatDistance = 40f;
    public float floatingDuration = 1.1f;
    public Vector2 floatingStartOffset = new Vector2(10f, -10f);

    [Header("Floating colours")]
    public Color floatingDamageColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color floatingHealColor = new Color(0.2f, 0.9f, 0.2f, 1f);

    [Header("Sunrise")]
    public TMP_Text timeToSunriseText;
    public RectTransform arcLeft;
    public RectTransform arcRight;
    public RectTransform moonIcon;
    public float moonArcHeight = 26f;

    [Header("Light bar")]
    public Image lightFill;
    public TMP_Text lightPercentText;

    [Header("Controls overlay")]
    public TMP_Text fixKeyText;

    bool hasLastHealth;
    float lastHealthValue;

    void Awake()
    {
        TryBindAll();
        BindHealthEvents();
        UpdateControlsText();
    }

    void OnEnable()
    {
        ClearFloatingTexts();
        BindHealthEvents();
    }

    void OnDisable()
    {
        ClearFloatingTexts();
        UnbindHealthEvents();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextRebindTime)
        {
            nextRebindTime = Time.unscaledTime + Mathf.Max(0.05f, rebindInterval);

            bool changed = TryBindAll();
            if (changed)
                BindHealthEvents();
        }

        UpdateSunrise();
        UpdateLightBar();
        UpdateControlsText();
    }

    bool TryBindAll()
    {
        bool changed = false;

        if (playerHealth == null)
        {
            var found = PlayerHealth.FindPlayerHealth();
            if (found != null) { playerHealth = found; changed = true; }
        }

        if (powerManager == null)
        {
            var found = PowerDecayManager.Instance;
            if (found != null) { powerManager = found; changed = true; }
        }

        if (repairInteraction == null)
        {
            var found = FindFirstObjectByType<PowerRepairInteraction>();
            if (found != null) { repairInteraction = found; changed = true; }
        }

        return changed;
    }

    void BindHealthEvents()
    {
        if (playerHealth == null) return;

        ClearFloatingTexts();

        playerHealth.OnHealthChanged -= OnHealthChanged;
        playerHealth.OnHealthDelta -= OnHealthDelta;

        playerHealth.OnHealthChanged += OnHealthChanged;
        playerHealth.OnHealthDelta += OnHealthDelta;

        hasLastHealth = false;

        OnHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    public void ClearFloatingTexts()
    {
        if (floatingSpawnRoot == null) return;

        Transform templateT = (floatingTextPrefab != null) ? floatingTextPrefab.transform : null;

        for (int i = floatingSpawnRoot.childCount - 1; i >= 0; i--)
        {
            var child = floatingSpawnRoot.GetChild(i);
            if (child == null) continue;

            // Do not delete the scene template
            if (templateT != null && child == templateT) continue;

            Destroy(child.gameObject);
        }

        // Keep template hidden
        if (floatingTextPrefab != null && floatingTextPrefab.gameObject.activeSelf)
            floatingTextPrefab.gameObject.SetActive(false);
    }


    void UnbindHealthEvents()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= OnHealthChanged;
        playerHealth.OnHealthDelta -= OnHealthDelta;

        hasLastHealth = false;
    }

    void OnHealthDelta(float delta)
    {
        if (Mathf.Abs(delta) < 0.0001f) return;

        if (delta < 0f)
            SpawnFloatingText(FormatNumber(delta), floatingDamageColor);
        else
            SpawnFloatingText("+" + FormatNumber(delta), floatingHealColor);
    }

    void OnHealthChanged(float current, float max)
    {
        if (hasLastHealth && Mathf.Abs(current - lastHealthValue) > 0.0001f)
        {
            float delta = current - lastHealthValue;

            if (delta < 0f) SpawnFloatingText(FormatNumber(delta), floatingDamageColor);
            else SpawnFloatingText("+" + FormatNumber(delta), floatingHealColor);
        }

        lastHealthValue = current;
        hasLastHealth = true;

        if (healthFill != null)
        {
            float t = (max <= 0.0001f) ? 0f : Mathf.Clamp01(current / max);
            healthFill.fillAmount = t;

            if (t <= redThreshold) healthFill.color = healthRed;
            else if (t <= orangeThreshold) healthFill.color = healthOrange;
            else healthFill.color = healthGreen;
        }

        if (hpLabelText != null) hpLabelText.text = "HP";

        if (hpValueText != null)
        {
            if (!hpShowAsFraction || max <= 0.0001f)
                hpValueText.text = FormatNumber(current);
            else
                hpValueText.text = FormatNumber(current) + "/" + FormatNumber(max);
        }
    }

    static string FormatNumber(float v)
    {
        float r = Mathf.Round(v);
        if (Mathf.Abs(v - r) < 0.0005f)
            return ((int)r).ToString(CultureInfo.InvariantCulture);

        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    void SpawnFloatingText(string message, Color color)
    {
        if (floatingSpawnRoot == null) return;

        var text = CreateFloatingInstance();
        if (text == null) return;

        text.text = message;
        text.color = color;

        StartCoroutine(AnimateFloatingText(text));
    }

    TextMeshProUGUI CreateFloatingInstance()
    {
        RectTransform root = floatingSpawnRoot;
        if (root == null) return null;

        if (floatingTextPrefab != null)
        {
            // If the "prefab" is a scene child, instantiate its GameObject
            // and then read TMP from the clone. This keeps your scene styling.
            var templateGO = floatingTextPrefab.gameObject;

            var cloneGO = Instantiate(templateGO, root);
            cloneGO.name = templateGO.name; // keep clean names
            cloneGO.SetActive(true);

            var instance = cloneGO.GetComponent<TextMeshProUGUI>();
            if (instance == null)
            {
                Destroy(cloneGO);
                return null;
            }

            // Reset only position so animation starts consistently.
            // Do not touch size/font, you want scene-driven look.
            instance.rectTransform.anchoredPosition3D = Vector3.zero;

            return instance;
        }

        // Fallback if no template is assigned
        var go = new GameObject("FloatingText");
        go.transform.SetParent(root, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Left;

        var styleSource = timeToSunriseText != null ? timeToSunriseText : hpValueText;

        if (styleSource != null)
        {
            text.font = styleSource.font;
            text.fontSharedMaterial = styleSource.fontSharedMaterial;
        }

        return text;
    }

    IEnumerator AnimateFloatingText(TextMeshProUGUI text)
    {
        if (text == null) yield break;

        RectTransform rect = text.rectTransform;

        Vector2 start = floatingStartOffset;
        Vector2 end = floatingStartOffset + Vector2.up * floatingFloatDistance;
        float elapsed = 0f;

        rect.anchoredPosition = start;

        while (elapsed < floatingDuration)
        {
            if (text == null) yield break;

            float t = Mathf.Clamp01(elapsed / floatingDuration);
            rect.anchoredPosition = Vector2.Lerp(start, end, t);
            text.alpha = 1f - t;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null)
            Destroy(text.gameObject);
    }

    void UpdateSunrise()
    {
        if (powerManager == null) return;
        if (timeToSunriseText == null) return;

        float session = Mathf.Max(0.0001f, powerManager.sessionLengthSeconds);
        float elapsed = powerManager.ElapsedSeconds;

        float remaining = Mathf.Max(0f, session - elapsed);
        timeToSunriseText.text = FormatMMSS(remaining);

        float t = Mathf.Clamp01(elapsed / session);
        UpdateMoonArc(t);
    }

    void UpdateMoonArc(float t01)
    {
        if (arcLeft == null || arcRight == null || moonIcon == null) return;

        t01 = Mathf.Clamp01(t01);

        Vector2 a = arcLeft.anchoredPosition;
        Vector2 b = arcRight.anchoredPosition;

        Vector2 mid = (a + b) * 0.5f;
        Vector2 control = mid + Vector2.up * moonArcHeight;

        Vector2 p0 = Vector2.Lerp(a, control, t01);
        Vector2 p1 = Vector2.Lerp(control, b, t01);
        Vector2 p = Vector2.Lerp(p0, p1, t01);

        moonIcon.anchoredPosition = p;
    }

    void UpdateLightBar()
    {
        if (lightFill == null && lightPercentText == null) return;

        float v = 1f;
        if (powerManager != null)
            v = powerManager.GetAverageLampPower01();

        v = Mathf.Clamp01(v);

        if (lightFill != null)
            lightFill.fillAmount = v;

        if (lightPercentText != null)
            lightPercentText.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    void UpdateControlsText()
    {
        if (fixKeyText != null)
        {
            Key k = Key.F;
            if (repairInteraction != null) k = repairInteraction.repairKey;
            fixKeyText.text = k.ToString().ToUpperInvariant();
        }
    }

    static string FormatMMSS(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        int mm = total / 60;
        int ss = total % 60;
        return Two(mm) + ":" + Two(ss);
    }

    static string Two(int v)
    {
        if (v < 0) v = 0;
        if (v < 10) return "0" + v;
        return v.ToString();
    }
}
