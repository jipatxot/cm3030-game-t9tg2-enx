using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


/// Tutorial popup that mirrors the logic of PowerRepairInteraction:
/// - Finds the nearest LightPowerDecay within a radius around the player (XZ distance).
/// - If it's a StreetLamp and is NOT fully lit, show a Screen Space Overlay popup near the player:
///     "Press F to turn on the light."
/// - If the player presses the repair key while the popup is visible, the tutorial is completed for this run
///   and will not show again.
/// - If the player does not press the key and leaves, it can show again next time.
///
/// Attach to ANY active GameObject (e.g., the same GameObject as PowerRepairInteraction or a UI manager).
/// Requires the player GameObject to have Tag = "Player" (same as PowerRepairInteraction).
public class RepairHintPopupUI : MonoBehaviour
{
    [Header("References (optional)")]
    [Tooltip("If empty, we try to find a PowerRepairInteraction in the scene and mirror its key/radius.")]
    public PowerRepairInteraction repairInteraction;

    [Tooltip("If empty, we find the Player by Tag 'Player'.")]
    public Transform playerOverride;

    [Header("Lamp filter")]
    [Tooltip("Only show for objects whose name contains this (e.g., StreetLamp(Clone)).")]
    public string requiredNameContains = "StreetLamp";

    [Header("When to show")]
    [Range(0f, 1f)]
    [Tooltip("If lamp power01 is below this, we consider it 'not fully lit'.")]
    public float fullyLitThreshold01 = 0.999f;

    [Tooltip("Also treat lamps as 'not fully lit' if their child Lights are disabled or very dim.")]
    public bool treatVisiblyDarkAsNotFullyLit = true;

    [Min(0f)] public float dimIntensityThreshold = 0.15f;

    [Header("UI")]
    public string messageFormat = "Press {0} to turn on the light.";
    public Vector2 screenOffset = new Vector2(0f, 90f);
    public int fontSize = 28;

    [Tooltip("Optional existing canvas. If null, a Screen Space Overlay canvas is created.")]
    public Canvas targetCanvas;

    [Tooltip("Sorting order when we auto-create a canvas.")]
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

    // UI
    Canvas _createdCanvas;
    GameObject _popupGO;
    RectTransform _popupRT;
    CanvasGroup _popupGroup;
    Text _popupText;

    Camera _cam;
    bool _loggedTriggered;

    // Cache child lights per lamp for the "visibly dark" check
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

        // Mirror key from repair script (default F)
        Key key = (repairInteraction != null) ? repairInteraction.repairKey : Key.F;

        // If key pressed while popup visible => complete tutorial for this run
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

        // Throttled scan (unscaled so it works even if timeScale=0)
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
        // Use XZ distance like PowerRepairInteraction (ignore y)
        float r2 = radius * radius;

        // We scan all lights including inactive (same as repair) to avoid missing at startup
        List<LightPowerDecay> list = GetAllIncludingInactive();

        LightPowerDecay best = null;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;

            // Filter only StreetLamp (by name)
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

    // ---------- UI helpers ----------

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
        _popupRT.sizeDelta = new Vector2(900f, 80f);

        _popupGroup = _popupGO.AddComponent<CanvasGroup>();
        _popupGroup.alpha = 0f;
        _popupGroup.interactable = false;
        _popupGroup.blocksRaycasts = false;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(_popupGO.transform, false);

        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        _popupText = textGO.AddComponent<Text>();
        _popupText.alignment = TextAnchor.MiddleCenter;
        _popupText.fontSize = fontSize;
        _popupText.color = Color.white;
        _popupText.raycastTarget = false;
        _popupText.font = LoadBuiltinFontSafe();
    }

    void ShowPopup(Key key)
    {
        if (_popupGroup != null) _popupGroup.alpha = 1f;

        if (_popupText != null)
        {
            string k = key.ToString(); // e.g., F
            string msg = string.Format(messageFormat, k);
            if (_popupText.text != msg) _popupText.text = msg;
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
            // Fallback: center-ish
            _popupRT.anchoredPosition = screenOffset;
            return;
        }

        if (!TryGetPlayer(out var player)) return;

        Vector3 screen = _cam.WorldToScreenPoint(player.position);

        // behind camera -> mirror
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
}
