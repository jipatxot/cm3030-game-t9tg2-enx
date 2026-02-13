using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public TMP_Text hpLabelText;          // e.g. "HP"
    public TMP_Text hpValueText;          // e.g. "87/100" or "87"
    public bool hpShowAsFraction = true;

    [Header("Floating damage (optional, no extra script)")]
    public RectTransform floatingSpawnRoot;          // set to your floating root (RectTransform)
    public TextMeshProUGUI floatingTextPrefab;       // optional
    public float floatingFloatDistance = 40f;
    public float floatingDuration = 1.1f;
    public Vector2 floatingStartOffset = new Vector2(10f, -10f);

    [Header("Floating colours")]
    public Color floatingDamageColor = new Color(0.9f, 0.2f, 0.2f, 1f); // red
    public Color floatingHealColor = new Color(0.2f, 0.9f, 0.2f, 1f);   // green

    [Header("Sunrise")]
    public TMP_Text timeToSunriseText;
    public RectTransform arcLeft;
    public RectTransform arcRight;
    public RectTransform moonIcon;
    public float moonArcHeight = 26f;

    [Header("Light bar")]
    public Image lightFill;

    [Header("Controls overlay")]
    public TMP_Text moveKeysText;
    public TMP_Text fixKeyText;

    bool hasLastHealth;
    int lastHealthValue;

    void Awake()
    {
        TryBindAll();
        BindHealthEvents();
        UpdateControlsText();
    }

    void OnEnable()
    {
        BindHealthEvents();
    }

    void OnDisable()
    {
        UnbindHealthEvents();
    }

    void Update()
    {
        // Rebind until everything exists (player spawns later)
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
            if (found != null)
            {
                playerHealth = found;
                changed = true;
            }
        }

        if (powerManager == null)
        {
            var found = PowerDecayManager.Instance;
            if (found != null)
            {
                powerManager = found;
                changed = true;
            }
        }

        if (repairInteraction == null)
        {
            var found = FindFirstObjectByType<PowerRepairInteraction>();
            if (found != null)
            {
                repairInteraction = found;
                changed = true;
            }
        }

        return changed;
    }

    void BindHealthEvents()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= OnHealthChanged;
        playerHealth.OnHealthChanged += OnHealthChanged;

        // Reset delta tracking when we (re)bind
        hasLastHealth = false;

        OnHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    void UnbindHealthEvents()
    {
        if (playerHealth == null) return;
        playerHealth.OnHealthChanged -= OnHealthChanged;
        hasLastHealth = false;
    }

    void OnHealthChanged(int current, int max)
    {
        // Floating numbers for damage and healing
        if (hasLastHealth && current != lastHealthValue)
        {
            int delta = current - lastHealthValue;

            if (delta < 0)
            {
                // damage
                SpawnFloatingText(delta.ToString(), floatingDamageColor);
            }
            else if (delta > 0)
            {
                // healing
                SpawnFloatingText("+" + delta, floatingHealColor);
            }
        }

        lastHealthValue = current;
        hasLastHealth = true;

        // Health fill
        if (healthFill != null)
        {
            float t = (max <= 0) ? 0f : Mathf.Clamp01((float)current / max);
            healthFill.fillAmount = t;

            if (t <= redThreshold) healthFill.color = healthRed;
            else if (t <= orangeThreshold) healthFill.color = healthOrange;
            else healthFill.color = healthGreen;
        }

        // Optional top-left HP
        if (hpLabelText != null) hpLabelText.text = "HP";
        if (hpValueText != null)
        {
            if (max <= 0) hpValueText.text = current.ToString();
            else hpValueText.text = hpShowAsFraction ? (current + "/" + max) : current.ToString();
        }
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
        RectTransform root = GetFloatingRoot();
        if (root == null) return null;

        if (floatingTextPrefab != null)
        {
            var instance = Instantiate(floatingTextPrefab, root);
            instance.gameObject.SetActive(true);
            return instance;
        }

        var go = new GameObject("FloatingText");
        go.transform.SetParent(root, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Left;

        // Try to match style with HUD text
        var styleSource = timeToSunriseText != null ? timeToSunriseText : (hpValueText != null ? hpValueText : moveKeysText);
        if (styleSource != null)
        {
            text.font = styleSource.font;
            text.fontSharedMaterial = styleSource.fontSharedMaterial;
        }

        return text;
    }

    RectTransform GetFloatingRoot()
    {
        if (floatingSpawnRoot != null) return floatingSpawnRoot;
        return null;
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
        if (lightFill == null) return;

        float v = 1f;
        if (powerManager != null)
            v = powerManager.GetAverageLampPower01();

        lightFill.fillAmount = Mathf.Clamp01(v);
    }

    void UpdateControlsText()
    {
        if (moveKeysText != null)
            moveKeysText.text = "W A S D";

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
