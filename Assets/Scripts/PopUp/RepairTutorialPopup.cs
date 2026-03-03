using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


/// Tutorial pop-up:
/// - When the player first moves near a NOT-fully-lit StreetLamp, show: "Press A to turn on the light."
/// - If the player presses A while the prompt is visible, the tutorial is marked complete and will not show again.
/// - If the player walks away without pressing A, it may show again next time they approach a dim lamp.
/// 
/// Setup:
/// 1) Add this component to your Player GameObject (the one that moves).
/// 2) Ensure StreetLamp instances have LightPowerDecay on the root.
/// 3) Optionally set useNameFilter/tag filter to target ONLY street lamps (and not traffic lights).
public class RepairTutorialPopup : MonoBehaviour
{
    private const string PrefsKey = "tutorial_pressF_light_completed";

    [Header("Input")]
    public Key repairKey = Key.F;

    [Header("Detection")]
    [Tooltip("How close the player must be to a lamp to show the prompt.")]
    [Min(0.1f)] public float searchRadius = 3.0f;

    [Tooltip("If lamp NormalizedPower01 is below this value, we consider it not fully lit.")]
    [Range(0f, 1f)] public float fullyLitThreshold01 = 0.999f;

    [Tooltip("Require that the player has moved at least this distance from the spawn point before showing the tutorial.")]
    public bool requirePlayerMoved = true;

    [Min(0f)] public float minMoveDistance = 0.25f;

    [Header("Lamp Filtering (optional)")]
    [Tooltip("If true, only show for lamps whose GameObject name contains this string (case-insensitive).")]
    public bool useNameFilter = true;

    public string requiredNameContains = "StreetLamp";

    [Tooltip("If true, only show for lamps whose tag equals lampTag.")]
    public bool useTagFilter = false;

    public string lampTag = "StreetLamp";

    [Header("Persistence (optional)")]
    [Tooltip("If true, the tutorial completion is saved in PlayerPrefs, so it won't show next run.")]
    public bool rememberAcrossRuns = false;

    [Header("UI (optional)")]
    [Tooltip("If null, a small world-space Canvas + Text will be auto-created and attached to the player.")]
    public Canvas worldCanvas;

    public Text messageText;

    [Tooltip("Popup offset relative to the player (world units).")]
    public Vector3 popupOffset = new Vector3(0f, 2.0f, 0f);

    [Tooltip("World-space UI scale (smaller = smaller text).")]
    [Min(0.0001f)] public float worldUIScale = 0.01f;

    [Header("Text")]
    public string promptMessage = "Press F to turn on the light.";

    [Tooltip("Text size (world-space canvas).")]
    [Min(1)] public int fontSize = 36;

    [Tooltip("Max width of the text box (world-space canvas local units).")]
    public Vector2 boxSize = new Vector2(600f, 120f);

    [Header("Behaviour")]
    public bool faceCamera = true;

    [Tooltip("How often (seconds) to scan lamps. 0.1-0.25 is fine.")]
    [Min(0.02f)] public float scanInterval = 0.15f;

    private Vector3 _spawnPos;
    private bool _completed;
    private bool _visible;
    private float _scanTimer;

    private LightPowerDecay _currentLamp;

    private void Awake()
    {
        _spawnPos = transform.position;

        if (rememberAcrossRuns)
            _completed = PlayerPrefs.GetInt(PrefsKey, 0) == 1;

        EnsureUI();
        SetVisible(false);
    }

    private void Update()
    {
        if (_completed)
        {
            if (_visible) SetVisible(false);
            return;
        }

        if (requirePlayerMoved)
        {
            float moved = (transform.position - _spawnPos).magnitude;
            if (moved < minMoveDistance)
            {
                if (_visible) SetVisible(false);
                return;
            }
        }

        // Scan for nearby lamps periodically (not every frame)
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            _currentLamp = FindNearestDimLamp();
            SetVisible(_currentLamp != null);
        }

        // If prompt is visible and player presses A, mark tutorial complete
        if (_visible && Keyboard.current != null)
        {
            var key = Keyboard.current[repairKey];
            if (key != null && key.wasPressedThisFrame)
            {
                CompleteTutorial();
            }
        }
    }

    private void LateUpdate()
    {
        if (!_visible) return;

        // Keep popup near player
        if (worldCanvas != null)
            worldCanvas.transform.position = transform.position + popupOffset;

        // Face camera
        if (faceCamera)
        {
            Camera cam = Camera.main;
            if (cam != null && worldCanvas != null)
            {
                worldCanvas.transform.rotation = cam.transform.rotation;
            }
        }
    }

    private void CompleteTutorial()
    {
        _completed = true;

        if (rememberAcrossRuns)
        {
            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
        }

        SetVisible(false);
    }

    private LightPowerDecay FindNearestDimLamp()
    {
        float r2 = searchRadius * searchRadius;
        LightPowerDecay best = null;
        float bestD2 = float.PositiveInfinity;

#if UNITY_2023_1_OR_NEWER
        var lamps = Object.FindObjectsByType<LightPowerDecay>(FindObjectsSortMode.None);
#else
        var lamps = Object.FindObjectsOfType<LightPowerDecay>();
#endif
        for (int i = 0; i < lamps.Length; i++)
        {
            var l = lamps[i];
            if (l == null) continue;

            // Filter to street lamps only (optional)
            if (useNameFilter && !NameContains(l.gameObject.name, requiredNameContains))
                continue;

            if (useTagFilter && l.gameObject.tag != lampTag)
                continue;

            // Only show when not fully lit
            if (l.NormalizedPower01 >= fullyLitThreshold01)
                continue;

            Vector3 d = l.transform.position - transform.position;
            d.y = 0f; // ignore height for top-down/isometric setups
            float d2 = d.sqrMagnitude;

            if (d2 <= r2 && d2 < bestD2)
            {
                bestD2 = d2;
                best = l;
            }
        }

        return best;
    }

    private static bool NameContains(string name, string contains)
    {
        if (string.IsNullOrEmpty(contains)) return true;
        if (string.IsNullOrEmpty(name)) return false;
        return name.ToLowerInvariant().Contains(contains.ToLowerInvariant());
    }

    private void EnsureUI()
    {
        if (worldCanvas != null && messageText != null)
        {
            messageText.text = promptMessage;
            return;
        }

        // Create a world-space canvas attached to player
        var canvasGO = new GameObject("TutorialPopupCanvas");
        canvasGO.transform.SetParent(transform, false);

        worldCanvas = canvasGO.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.transform.localScale = Vector3.one * worldUIScale;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create a background (optional, subtle)
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.raycastTarget = false;
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        var bgRT = bg.rectTransform;
        bgRT.sizeDelta = boxSize;
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.pivot = new Vector2(0.5f, 0.5f);
        bgRT.anchoredPosition = Vector2.zero;

        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);

        messageText = textGO.AddComponent<Text>();
        messageText.raycastTarget = false;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.fontSize = fontSize;
        messageText.text = promptMessage;
        messageText.color = Color.white;

        // Font compatibility: Unity 6 prefers LegacyRuntime.ttf
        messageText.font = LoadBuiltinFontSafe();

        var rt = messageText.rectTransform;
        rt.sizeDelta = boxSize;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private static Font LoadBuiltinFontSafe()
    {
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        return null;
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;

        if (worldCanvas != null)
            worldCanvas.gameObject.SetActive(visible);
    }
}
