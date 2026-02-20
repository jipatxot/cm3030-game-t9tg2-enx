using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sunrise / "morning has come" finale effect:
/// - Fades in a warm full-screen overlay (atmosphere / glow)
/// - Optionally ramps up a scene Light intensity + color
/// - Optionally brightens ambient lighting
/// - (NEW) Spawns an optional sun prefab at the top of the screen / scene and fades it in
///
/// Designed to work even if Time.timeScale == 0 (uses unscaled time).
///
/// Restart / main menu:
/// Call StopAndReset() before restarting, OR enable autoResetOnDisable so the effect cleans itself up
/// when the win panel gets hidden / destroyed.
/// </summary>
public class SunriseFinaleEffect : MonoBehaviour
{
    [Header("Play")]
    public bool playOnStart = false;
    public bool playOnEnable = true;
    public bool ignoreTimeScale = true;

    [Header("Cleanup")]
    [Tooltip("If true, when this component is disabled, it will stop and reset lighting/overlay back to the original values.")]
    public bool autoResetOnDisable = true;

    [Tooltip("If true, also DESTROY the auto-created overlay objects on reset.")]
    public bool destroyOverlayOnReset = true;

    [Header("Timing")]
    [Min(0.05f)] public float fadeInSeconds = 4.0f;
    [Min(0f)] public float holdSeconds = 0.0f;
    [Min(0.05f)] public float fadeOutSeconds = 0.0f;

    [Header("Overlay (Atmosphere)")]
    public bool enableOverlay = true;
    [Range(0f, 1f)] public float overlayMaxAlpha = 0.35f;
    public Color overlayColor = new Color(1f, 0.78f, 0.52f, 1f);

    [Tooltip("If empty, the script will auto-create a full-screen overlay under the nearest Canvas.")]
    public CanvasGroup overlayGroup;
    public Image overlayImage;

    [Tooltip("If we must create a new Canvas, its sorting order. Use a negative value to render BEHIND other UI.")]
    public int overlayCanvasSortingOrder = -10;

    [Header("Sun Prefab (Optional)")]
    [Tooltip("Optional sun prefab. If assigned, it will spawn when sunrise starts and be removed on reset.")]
    public GameObject sunPrefab;

    [Tooltip("If assigned, the sun prefab is parented here. Leave empty to auto-pick a Canvas (UI) or scene root.")]
    public Transform sunParent;

    [Tooltip("If true and the prefab has a RectTransform, place it as a UI element under a Canvas.")]
    public bool placeSunAsUI = true;

    [Tooltip("Top-center UI position (anchored). Negative Y moves downward.")]
    public Vector2 sunUIAnchoredPosition = new Vector2(0f, -90f);

    [Tooltip("UI size if the prefab root has a RectTransform.")]
    public Vector2 sunUISize = new Vector2(220f, 220f);

    [Tooltip("Fallback world position if the prefab is not a UI prefab.")]
    public Vector3 sunWorldPosition = new Vector3(0f, 8f, 0f);

    [Tooltip("Fallback world scale if the prefab is not a UI prefab.")]
    public Vector3 sunWorldScale = Vector3.one;

    [Tooltip("Fade in/out the spawned sun using CanvasGroup or SpriteRenderer alpha if possible.")]
    public bool fadeSunWithSunrise = true;

    [Tooltip("Subtle size growth during sunrise.")]
    public bool animateSunScale = true;

    [Min(0.1f)] public float sunStartScaleMultiplier = 0.85f;
    [Min(0.1f)] public float sunEndScaleMultiplier = 1.0f;

    [Tooltip("Destroy the spawned sun instance on reset/restart.")]
    public bool destroySunOnReset = true;

    [Header("Scene Lighting (Optional)")]
    public bool affectDirectionalLight = true;
    [Tooltip("Assign a Light (Directional recommended). If null, we try to find the first Directional light.")]
    public Light targetLight;

    public Color sunriseLightColor = new Color(1f, 0.92f, 0.75f, 1f);
    [Min(0f)] public float startLightIntensity = 0.0f;
    [Min(0f)] public float endLightIntensity = 1.2f;

    [Header("Ambient (Optional)")]
    public bool affectAmbient = true;
    public Color ambientEndColor = new Color(1f, 0.96f, 0.90f, 1f);
    [Min(0f)] public float ambientIntensityEnd = 1.0f;

    [Header("Easing")]
    public AnimationCurve ease01 = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    bool _played;
    bool _capturedOriginals;

    float _ambientIntensityStart;
    Color _ambientColorStart;

    float _lightIntensityStart;
    Color _lightColorStart;

    GameObject _createdOverlayGO;
    GameObject _createdCanvasGO;

    GameObject _spawnedSun;
    CanvasGroup _sunCanvasGroup;
    SpriteRenderer _sunSpriteRenderer;
    Graphic[] _sunGraphics;
    Color[] _sunGraphicBaseColors;
    Vector3 _sunBaseScale = Vector3.one;

    void Start()
    {
        if (playOnStart) Play();
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        if (autoResetOnDisable)
            StopAndReset(destroyOverlayOnReset);
    }

    public void Play()
    {
        if (_played) return;
        _played = true;

        EnsureOverlay();
        EnsureSunInstance();
        CaptureOriginalsIfNeeded();

        if (affectDirectionalLight)
        {
            if (targetLight == null)
                targetLight = FindFirstDirectionalLight();

            if (targetLight != null)
                targetLight.intensity = startLightIntensity;
        }

        StopAllCoroutines();
        StartCoroutine(CoSunrise());
    }

    public void StopAndReset(bool destroyOverlay = true)
    {
        StopAllCoroutines();

        if (overlayGroup != null)
            overlayGroup.alpha = 0f;

        ResetSpawnedSunVisuals();
        if (destroySunOnReset)
            DestroySpawnedSun();

        if (_capturedOriginals && affectAmbient)
        {
            RenderSettings.ambientLight = _ambientColorStart;
            RenderSettings.ambientIntensity = _ambientIntensityStart;
        }

        if (_capturedOriginals && affectDirectionalLight && targetLight != null)
        {
            targetLight.color = _lightColorStart;
            targetLight.intensity = _lightIntensityStart;
        }

        if (destroyOverlay)
        {
            if (_createdOverlayGO != null) Destroy(_createdOverlayGO);
            if (_createdCanvasGO != null) Destroy(_createdCanvasGO);
            _createdOverlayGO = null;
            _createdCanvasGO = null;
            overlayGroup = null;
            overlayImage = null;
        }

        _played = false;
    }

    System.Collections.IEnumerator CoSunrise()
    {
        yield return Fade(0f, 1f, fadeInSeconds);

        if (holdSeconds > 0f)
            yield return Wait(holdSeconds);

        if (fadeOutSeconds > 0f)
            yield return Fade(1f, 0f, fadeOutSeconds);
    }

    System.Collections.IEnumerator Fade(float a0, float a1, float seconds)
    {
        seconds = Mathf.Max(0.0001f, seconds);
        float t = 0f;

        while (t < seconds)
        {
            float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float u = Mathf.Clamp01(t / seconds);
            float e = ease01 != null ? ease01.Evaluate(u) : u;

            Apply(e, a0, a1);
            yield return null;
        }

        Apply(1f, a0, a1);
    }

    System.Collections.IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            yield return null;
        }
    }

    void Apply(float eased01, float a0, float a1)
    {
        float k = Mathf.Lerp(a0, a1, eased01);

        if (enableOverlay && overlayGroup != null)
            overlayGroup.alpha = k * overlayMaxAlpha;

        ApplySunVisuals(k);

        if (affectAmbient)
        {
            RenderSettings.ambientLight = Color.Lerp(_ambientColorStart, ambientEndColor, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(_ambientIntensityStart, ambientIntensityEnd, k);
        }

        if (affectDirectionalLight && targetLight != null)
        {
            targetLight.color = Color.Lerp(_lightColorStart, sunriseLightColor, k);
            targetLight.intensity = Mathf.Lerp(startLightIntensity, endLightIntensity, k);
        }
    }

    void CaptureOriginalsIfNeeded()
    {
        if (_capturedOriginals) return;

        if (affectAmbient)
        {
            _ambientColorStart = RenderSettings.ambientLight;
            _ambientIntensityStart = RenderSettings.ambientIntensity;
        }

        if (affectDirectionalLight)
        {
            if (targetLight == null)
                targetLight = FindFirstDirectionalLight();

            if (targetLight != null)
            {
                _lightIntensityStart = targetLight.intensity;
                _lightColorStart = targetLight.color;
            }
        }

        _capturedOriginals = true;
    }

    void EnsureOverlay()
    {
        if (!enableOverlay) return;

        if (overlayGroup != null && overlayImage != null)
        {
            overlayImage.raycastTarget = false;
            overlayImage.color = overlayColor;
            overlayGroup.alpha = 0f;
            return;
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            parentCanvas = FindAnyCanvas();

        if (parentCanvas == null)
        {
            _createdCanvasGO = new GameObject("SunriseOverlayCanvas");
            parentCanvas = _createdCanvasGO.AddComponent<Canvas>();
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            parentCanvas.sortingOrder = overlayCanvasSortingOrder;

            _createdCanvasGO.AddComponent<CanvasScaler>();
            _createdCanvasGO.AddComponent<GraphicRaycaster>();
        }

        _createdOverlayGO = new GameObject("SunriseOverlay");
        _createdOverlayGO.transform.SetParent(parentCanvas.transform, false);
        _createdOverlayGO.transform.SetAsFirstSibling();

        var rt = _createdOverlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        overlayImage = _createdOverlayGO.AddComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = false;

        overlayGroup = _createdOverlayGO.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;
    }

    void EnsureSunInstance()
    {
        if (sunPrefab == null) return;
        if (_spawnedSun != null) return;

        Transform parent = sunParent;

        if (parent == null && placeSunAsUI)
        {
            var c = GetComponentInParent<Canvas>();
            if (c == null) c = FindAnyCanvas();
            if (c != null) parent = c.transform;
        }

        if (parent != null)
            _spawnedSun = Instantiate(sunPrefab, parent, false);
        else
            _spawnedSun = Instantiate(sunPrefab);

        _spawnedSun.name = sunPrefab.name + "_RuntimeSun";

        bool isUI = false;
        var rt = _spawnedSun.GetComponent<RectTransform>();
        if (rt != null && placeSunAsUI)
        {
            isUI = true;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = sunUIAnchoredPosition;
            rt.sizeDelta = sunUISize;

            if (_createdOverlayGO != null && _spawnedSun.transform.parent == _createdOverlayGO.transform.parent)
                _spawnedSun.transform.SetSiblingIndex(Mathf.Min(1, _spawnedSun.transform.parent.childCount - 1));
        }
        else
        {
            _spawnedSun.transform.position = sunWorldPosition;
            _spawnedSun.transform.localScale = sunWorldScale;
        }

        _sunBaseScale = _spawnedSun.transform.localScale;

        _sunCanvasGroup = _spawnedSun.GetComponent<CanvasGroup>();
        if (_sunCanvasGroup == null && isUI)
            _sunCanvasGroup = _spawnedSun.AddComponent<CanvasGroup>();

        _sunSpriteRenderer = _spawnedSun.GetComponentInChildren<SpriteRenderer>(true);
        _sunGraphics = _spawnedSun.GetComponentsInChildren<Graphic>(true);

        if (_sunGraphics != null && _sunGraphics.Length > 0)
        {
            _sunGraphicBaseColors = new Color[_sunGraphics.Length];
            for (int i = 0; i < _sunGraphics.Length; i++)
                _sunGraphicBaseColors[i] = _sunGraphics[i].color;
        }

        ApplySunVisuals(0f);
    }

    void ApplySunVisuals(float k01)
    {
        if (_spawnedSun == null) return;

        float alpha = fadeSunWithSunrise ? Mathf.Clamp01(k01) : 1f;

        if (_sunCanvasGroup != null)
            _sunCanvasGroup.alpha = alpha;

        if (_sunSpriteRenderer != null)
        {
            Color c = _sunSpriteRenderer.color;
            c.a = alpha;
            _sunSpriteRenderer.color = c;
        }

        if (_sunGraphics != null && _sunGraphicBaseColors != null)
        {
            for (int i = 0; i < _sunGraphics.Length; i++)
            {
                if (_sunGraphics[i] == null) continue;
                Color c = _sunGraphicBaseColors[i];
                c.a = _sunGraphicBaseColors[i].a * alpha;
                _sunGraphics[i].color = c;
            }
        }

        if (animateSunScale)
        {
            float s = Mathf.Lerp(sunStartScaleMultiplier, sunEndScaleMultiplier, Mathf.Clamp01(k01));
            _spawnedSun.transform.localScale = _sunBaseScale * s;
        }
    }

    void ResetSpawnedSunVisuals()
    {
        if (_spawnedSun == null) return;
        ApplySunVisuals(0f);
    }

    void DestroySpawnedSun()
    {
        if (_spawnedSun != null)
            Destroy(_spawnedSun);

        _spawnedSun = null;
        _sunCanvasGroup = null;
        _sunSpriteRenderer = null;
        _sunGraphics = null;
        _sunGraphicBaseColors = null;
        _sunBaseScale = Vector3.one;
    }

    static Canvas FindAnyCanvas()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
#else
        return FindObjectOfType<Canvas>(true);
#endif
    }

    static Light FindFirstDirectionalLight()
    {
        var lights = FindObjectsOfType<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l != null && l.type == LightType.Directional)
                return l;
        }
        return null;
    }
}
