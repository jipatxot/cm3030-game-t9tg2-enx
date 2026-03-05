using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;


/// Tutorial popup that mirrors the logic of PowerRepairInteraction:
/// - Finds the nearest LightPowerDecay within a radius around the player (XZ distance).
/// - If it's a StreetLamp and is NOT fully lit, show a Screen Space Overlay popup near the player:
///     "Press F to turn on the light."
/// - If the player presses the repair key while the popup is visible, the tutorial is completed for this run
///   and will not show again.
/// - If the player does not press the key and leaves, it can show again next time.
///
/// UI Improvements:
/// - Rounded-corner panel (uses Unity built-in sliced UI sprites)
/// - Semi-transparent white background (alpha ~ 40%)
/// - Border (separate sliced image behind the background)
///
/// Attach to ANY active GameObject (e.g., the same GameObject as PowerRepairInteraction or a UI manager).
/// Requires the player GameObject to have Tag = "Player" (same as PowerRepairInteraction).

public class RepairHintPopupUI : MonoBehaviour
{
    [Header("References (optional)")]
    public PowerRepairInteraction repairInteraction;
    public Transform playerOverride;

    [Header("Lamp filter")]
    public string requiredNameContains = "StreetLamp";

    [Header("When to show")]
    [Range(0f, 1f)] public float fullyLitThreshold01 = 0.999f;
    public bool treatVisiblyDarkAsNotFullyLit = true;
    [Min(0f)] public float dimIntensityThreshold = 0.15f;

    [Header("UI - TMP")]
    public TMP_FontAsset tmpFont;
    public FontStyles tmpStyle = FontStyles.Bold;

    [Header("UI - Text")]
    public string messageFormat = "Press {0} to fix the lamp post.";
    public Vector2 screenOffset = new Vector2(0f, 90f);
    public int fontSize = 28;
    public Color textColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    [Header("UI - Panel (Rounded)")]
    public bool enablePanel = true;
    public Color panelBackgroundColor = new Color(1f, 1f, 1f, 0.40f);
    public Color panelBorderColor = new Color(0f, 0f, 0f, 0.55f);
    [Min(0f)] public float borderThickness = 4f;
    public Vector4 textPadding = new Vector4(18f, 10f, 18f, 10f);

    [Header("UI - Auto size")]
    [Min(0f)] public float maxPanelWidth = 700f;     // 0 = no limit
    public Vector2 minPanelSize = new Vector2(140f, 54f);

    public Canvas targetCanvas;
    public int createdCanvasSortingOrder = 9999;

    [Header("Scan timing")]
    [Min(0.02f)] public float scanInterval = 0.12f;

    [Header("Camera (for world->screen)")]
    public Camera preferredCamera;
    [Min(0f)] public float screenEdgePadding = 20f;

    [Header("Debug")]
    public bool logOnceWhenTriggered = false;

    bool _tutorialCompleted;
    float _scanTimer;

    Canvas _createdCanvas;
    GameObject _popupGO;
    RectTransform _popupRT;
    CanvasGroup _popupGroup;

    RectTransform _bgRT;
    RectTransform _textRT;

    Image _borderImage;
    Image _bgImage;
    TextMeshProUGUI _popupText;

    Camera _cam;
    bool _loggedTriggered;

    readonly Dictionary<LightPowerDecay, Light[]> _lampLightsCache = new Dictionary<LightPowerDecay, Light[]>();

    void Awake()
    {
        if (repairInteraction == null)
        {
#if UNITY_2023_1_OR_NEWER
            repairInteraction = FindFirstObjectByType<PowerRepairInteraction>(FindObjectsInactive.Include);
#else
            repairInteraction = FindObjectOfType<PowerRepairInteraction>(true);
#endif
        }

        EnsureUI();
        HidePopupImmediate();
        _cam = ChooseCamera();
    }

    void OnDestroy()
    {
        if (_createdCanvas != null)
        {
            Destroy(_createdCanvas.gameObject);
            _createdCanvas = null;
        }
    }

    void Update()
    {
        if (_tutorialCompleted)
        {
            HidePopupImmediate();
            return;
        }

        Key key = (repairInteraction != null) ? repairInteraction.repairKey : Key.F;

        var kb = Keyboard.current;
        if (kb != null && kb[key].wasPressedThisFrame)
        {
            if (IsPopupVisible())
            {
                _tutorialCompleted = true;
                HidePopupImmediate();
                return;
            }
        }

        _scanTimer += Time.unscaledDeltaTime;
        if (_scanTimer < scanInterval)
        {
            if (IsPopupVisible()) UpdatePopupPosition();
            return;
        }
        _scanTimer = 0f;

        if (!TryGetPlayer(out var player))
        {
            HidePopup();
            return;
        }

        float radius = (repairInteraction != null) ? repairInteraction.repairRadius : 2.2f;

        var lamp = FindNearestLampNeedingRepair(player.position, radius);
        if (lamp != null)
        {
            ShowPopup(key);
            UpdatePopupPosition();

            if (logOnceWhenTriggered && !_loggedTriggered)
            {
                _loggedTriggered = true;
                Debug.Log($"[RepairHintPopupUI] Triggered by '{lamp.name}', power01={lamp.NormalizedPower01:0.000}, visuallyDark={IsVisiblyDark(lamp)}");
            }
        }
        else
        {
            HidePopup();
        }
    }

    bool TryGetPlayer(out Transform p)
    {
        if (playerOverride != null) { p = playerOverride; return true; }

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) { p = null; return false; }

        p = go.transform;
        return true;
    }

    LightPowerDecay FindNearestLampNeedingRepair(Vector3 playerPos, float radius)
    {
        float r2 = radius * radius;
        List<LightPowerDecay> list = GetAllIncludingInactive();

        LightPowerDecay best = null;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;

            if (!string.IsNullOrEmpty(requiredNameContains))
            {
                string n = d.gameObject.name;
                if (n == null || !n.Contains(requiredNameContains))
                    continue;
            }

            if (!NeedsRepair(d))
                continue;

            Vector3 pos = d.transform.position;
            pos.y = playerPos.y;

            float d2 = (pos - playerPos).sqrMagnitude;
            if (d2 > r2) continue;

            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = d;
            }
        }

        return best;
    }

    bool NeedsRepair(LightPowerDecay d)
    {
        if (d == null) return false;

        if (d.NormalizedPower01 < fullyLitThreshold01)
            return true;

        if (treatVisiblyDarkAsNotFullyLit && IsVisiblyDark(d))
            return true;

        return false;
    }

    bool IsVisiblyDark(LightPowerDecay d)
    {
        if (d == null) return false;

        if (!_lampLightsCache.TryGetValue(d, out var lights) || lights == null)
        {
            lights = d.GetComponentsInChildren<Light>(true);
            _lampLightsCache[d] = lights;
        }

        if (lights == null || lights.Length == 0)
            return false;

        float maxI = 0f;
        bool anyEnabled = false;

        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;

            if (l.enabled) anyEnabled = true;
            if (l.intensity > maxI) maxI = l.intensity;
        }

        if (!anyEnabled) return true;
        return maxI <= dimIntensityThreshold;
    }

    static List<LightPowerDecay> GetAllIncludingInactive()
    {
#if UNITY_2023_1_OR_NEWER
        var found = Object.FindObjectsByType<LightPowerDecay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var found = Object.FindObjectsOfType<LightPowerDecay>(true);
#endif
        return new List<LightPowerDecay>(found);
    }

    void EnsureUI()
    {
        if (_popupGO != null && _popupText != null && _popupGroup != null)
            return;

        Canvas canvas = targetCanvas;

        if (canvas == null)
        {
#if UNITY_2023_1_OR_NEWER
            canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
#else
            canvas = Object.FindObjectOfType<Canvas>(true);
#endif
        }

        if (canvas == null)
        {
            var canvasGO = new GameObject("RepairHintCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = createdCanvasSortingOrder;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            _createdCanvas = canvas;
        }

        _popupGO = new GameObject("RepairHintPopup");
        _popupGO.transform.SetParent(canvas.transform, false);

        _popupRT = _popupGO.AddComponent<RectTransform>();
        _popupRT.anchorMin = new Vector2(0.5f, 0.5f);
        _popupRT.anchorMax = new Vector2(0.5f, 0.5f);
        _popupRT.pivot = new Vector2(0.5f, 0.5f);
        _popupRT.sizeDelta = minPanelSize;

        _popupGroup = _popupGO.AddComponent<CanvasGroup>();
        _popupGroup.alpha = 0f;
        _popupGroup.interactable = false;
        _popupGroup.blocksRaycasts = false;

        Sprite rounded = LoadRoundedUISpriteSafe();

        if (enablePanel)
        {
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(_popupGO.transform, false);
            var brt = borderGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            _borderImage = borderGO.AddComponent<Image>();
            _borderImage.sprite = rounded;
            _borderImage.type = Image.Type.Sliced;
            _borderImage.color = panelBorderColor;
            _borderImage.raycastTarget = false;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(_popupGO.transform, false);
            _bgRT = bgGO.AddComponent<RectTransform>();
            _bgRT.anchorMin = Vector2.zero;
            _bgRT.anchorMax = Vector2.one;
            _bgRT.offsetMin = new Vector2(borderThickness, borderThickness);
            _bgRT.offsetMax = new Vector2(-borderThickness, -borderThickness);

            _bgImage = bgGO.AddComponent<Image>();
            _bgImage.sprite = rounded;
            _bgImage.type = Image.Type.Sliced;
            _bgImage.color = panelBackgroundColor;
            _bgImage.raycastTarget = false;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(bgGO.transform, false);

            _textRT = textGO.AddComponent<RectTransform>();
            _textRT.anchorMin = Vector2.zero;
            _textRT.anchorMax = Vector2.one;
            _textRT.offsetMin = new Vector2(textPadding.x, textPadding.y);
            _textRT.offsetMax = new Vector2(-textPadding.z, -textPadding.w);

            _popupText = textGO.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(_popupGO.transform, false);

            _textRT = textGO.AddComponent<RectTransform>();
            _textRT.anchorMin = Vector2.zero;
            _textRT.anchorMax = Vector2.one;
            _textRT.offsetMin = Vector2.zero;
            _textRT.offsetMax = Vector2.zero;

            _popupText = textGO.AddComponent<TextMeshProUGUI>();
        }

        _popupText.raycastTarget = false;
        _popupText.font = tmpFont;               // assign in Inspector
        _popupText.fontSize = fontSize;
        _popupText.color = textColor;
        _popupText.fontStyle = tmpStyle;
        _popupText.alignment = TextAlignmentOptions.Center;
        _popupText.enableWordWrapping = false;   // you already handle wrapping via sizing logic
    }

    void ShowPopup(Key key)
    {
        if (_popupGroup != null) _popupGroup.alpha = 1f;

        if (_popupText != null)
        {
            string k = key.ToString();
            string msg = string.Format(messageFormat, k);
            if (_popupText.text != msg) _popupText.text = msg;
        }

        if (_borderImage != null) _borderImage.color = panelBorderColor;
        if (_bgImage != null) _bgImage.color = panelBackgroundColor;
        if (_popupText != null) _popupText.color = textColor;

        UpdatePanelSizeToText();
    }

    void UpdatePanelSizeToText()
    {
        if (_popupRT == null || _popupText == null) return;

        float availableForText = maxPanelWidth > 0f
            ? Mathf.Max(10f, maxPanelWidth - (enablePanel ? borderThickness * 2f : 0f) - textPadding.x - textPadding.z)
            : float.PositiveInfinity;

        _popupText.enableWordWrapping = (_popupText.preferredWidth > availableForText);

        Canvas.ForceUpdateCanvases();

        float textW = _popupText.preferredWidth;
        float textH = _popupText.preferredHeight;

        float w = textW + textPadding.x + textPadding.z + (enablePanel ? borderThickness * 2f : 0f);
        float h = textH + textPadding.y + textPadding.w + (enablePanel ? borderThickness * 2f : 0f);

        if (maxPanelWidth > 0f) w = Mathf.Min(w, maxPanelWidth);

        w = Mathf.Max(w, minPanelSize.x);
        h = Mathf.Max(h, minPanelSize.y);

        _popupRT.sizeDelta = new Vector2(w, h);

        if (_bgRT != null)
        {
            _bgRT.offsetMin = new Vector2(borderThickness, borderThickness);
            _bgRT.offsetMax = new Vector2(-borderThickness, -borderThickness);
        }

        if (_textRT != null)
        {
            _textRT.offsetMin = new Vector2(textPadding.x, textPadding.y);
            _textRT.offsetMax = new Vector2(-textPadding.z, -textPadding.w);
        }
    }

    void HidePopup()
    {
        if (_popupGroup != null) _popupGroup.alpha = 0f;
    }

    void HidePopupImmediate()
    {
        if (_popupGroup != null) _popupGroup.alpha = 0f;
    }

    bool IsPopupVisible()
    {
        return _popupGroup != null && _popupGroup.alpha > 0.5f;
    }

    Camera ChooseCamera()
    {
        if (preferredCamera != null && preferredCamera.enabled && preferredCamera.gameObject.activeInHierarchy)
            return preferredCamera;

        var main = Camera.main;
        if (main != null && main.enabled && main.gameObject.activeInHierarchy)
            return main;

#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var cams = Object.FindObjectsOfType<Camera>();
#endif
        Camera best = null;
        float bestDepth = float.NegativeInfinity;

        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null) continue;
            if (!c.enabled || !c.gameObject.activeInHierarchy) continue;

            if (c.depth >= bestDepth)
            {
                bestDepth = c.depth;
                best = c;
            }
        }

        return best;
    }

    void UpdatePopupPosition()
    {
        if (_popupRT == null) return;

        if (_cam == null || !_cam.enabled || !_cam.gameObject.activeInHierarchy)
            _cam = ChooseCamera();

        if (_cam == null)
        {
            _popupRT.anchoredPosition = screenOffset;
            return;
        }

        if (!TryGetPlayer(out var player)) return;

        Vector3 screen = _cam.WorldToScreenPoint(player.position);

        if (screen.z < 0f)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
            screen.z = 0.001f;
        }

        screen.x += screenOffset.x;
        screen.y += screenOffset.y;

        screen.x = Mathf.Clamp(screen.x, screenEdgePadding, Screen.width - screenEdgePadding);
        screen.y = Mathf.Clamp(screen.y, screenEdgePadding, Screen.height - screenEdgePadding);

        Canvas canvas = _popupGO != null ? _popupGO.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            _popupRT.position = new Vector3(screen.x, screen.y, 0f);
            return;
        }

        RectTransform canvasRT = canvas.transform as RectTransform;
        if (canvasRT == null)
        {
            _popupRT.position = new Vector3(screen.x, screen.y, 0f);
            return;
        }

        Camera uiCam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            uiCam = canvas.worldCamera != null ? canvas.worldCamera : _cam;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, new Vector2(screen.x, screen.y), uiCam, out Vector2 local))
        {
            _popupRT.anchoredPosition = local;
        }
    }

    static Font LoadBuiltinFontSafe()
    {
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        return null;
    }

    static Sprite LoadRoundedUISpriteSafe()
    {
        try { return Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd"); } catch { }
        try { return Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd"); } catch { }
        return null;
    }
}
