using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


/// Captures the "Main Menu" visuals so we can restore them after a win sunrise sequence.
/// Attach to StartPanel (Main Menu panel) so capture happens when the game first shows the menu.
public class MenuVisualSnapshot : MonoBehaviour
{
    public static MenuVisualSnapshot Instance { get; private set; }

    [Header("Capture")]
    public bool captureOnAwake = true;

    [Tooltip("If true, also snapshot likely sky renderers by shader name keywords.")]
    public bool captureSkyRenderers = true;

    public string[] skyShaderKeywords = new string[] { "Sky", "Cartoon", "Toon", "Atmos", "Cloud" };

    [Header("Debug")]
    public bool logOnCapture = false;
    public bool logOnRestore = false;

    bool _captured;

    AmbientMode _ambientMode;
    Color _ambientLight;
    float _ambientIntensity;

    bool _fog;
    Color _fogColor;
    float _fogDensity;
    FogMode _fogMode;

    Material _skyboxRef;
    Material _skyboxTemplate;

    [Serializable]
    class DirLightState
    {
        public Light light;
        public bool enabled;
        public float intensity;
        public Color color;
        public Quaternion rotation;
    }
    readonly List<DirLightState> _dirLights = new List<DirLightState>();

    [Serializable]
    class RendererMatState
    {
        public Renderer renderer;
        public Material[] materialRefs;
        public Material[] templates;
    }
    readonly List<RendererMatState> _skyRenderers = new List<RendererMatState>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (captureOnAwake)
            Capture();
    }

    public void Capture()
    {
        if (_captured) return;

        _ambientMode = RenderSettings.ambientMode;
        _ambientLight = RenderSettings.ambientLight;
        _ambientIntensity = RenderSettings.ambientIntensity;

        _fog = RenderSettings.fog;
        _fogColor = RenderSettings.fogColor;
        _fogDensity = RenderSettings.fogDensity;
        _fogMode = RenderSettings.fogMode;

        _skyboxRef = RenderSettings.skybox;
        _skyboxTemplate = (_skyboxRef != null) ? new Material(_skyboxRef) : null;

        _dirLights.Clear();
#if UNITY_2023_1_OR_NEWER
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var lights = FindObjectsOfType<Light>(true);
#endif
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;
            if (l.type != LightType.Directional) continue;

            _dirLights.Add(new DirLightState
            {
                light = l,
                enabled = l.enabled,
                intensity = l.intensity,
                color = l.color,
                rotation = l.transform.rotation
            });
        }

        _skyRenderers.Clear();
        if (captureSkyRenderers)
            CaptureSkyRenderersInternal();

        _captured = true;

        if (logOnCapture)
            Debug.Log($"[MenuVisualSnapshot] Captured menu visuals. DirLights={_dirLights.Count}, SkyRenderers={_skyRenderers.Count}");
    }

    void CaptureSkyRenderersInternal()
    {
#if UNITY_2023_1_OR_NEWER
        var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var renderers = FindObjectsOfType<Renderer>(true);
#endif
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) continue;

            bool looksLikeSky = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;

                string shaderName = mat.shader != null ? mat.shader.name : "";
                if (MatchesAnyKeyword(shaderName, skyShaderKeywords))
                {
                    looksLikeSky = true;
                    break;
                }
            }

            if (!looksLikeSky) continue;

            var state = new RendererMatState();
            state.renderer = r;
            state.materialRefs = mats;
            state.templates = new Material[mats.Length];

            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                state.templates[m] = (mat != null) ? new Material(mat) : null;
            }

            _skyRenderers.Add(state);
            if (_skyRenderers.Count >= 16) break;
        }
    }

    public void Restore()
    {
        if (!_captured) return;

        RenderSettings.ambientMode = _ambientMode;
        RenderSettings.ambientLight = _ambientLight;
        RenderSettings.ambientIntensity = _ambientIntensity;

        RenderSettings.fog = _fog;
        RenderSettings.fogColor = _fogColor;
        RenderSettings.fogDensity = _fogDensity;
        RenderSettings.fogMode = _fogMode;

        RenderSettings.skybox = _skyboxRef;
        if (RenderSettings.skybox != null && _skyboxTemplate != null)
        {
            RenderSettings.skybox.CopyPropertiesFromMaterial(_skyboxTemplate);
        }

        for (int i = 0; i < _dirLights.Count; i++)
        {
            var s = _dirLights[i];
            if (s == null || s.light == null) continue;

            s.light.enabled = s.enabled;
            s.light.intensity = s.intensity;
            s.light.color = s.color;
            s.light.transform.rotation = s.rotation;
        }

        for (int i = 0; i < _skyRenderers.Count; i++)
        {
            var s = _skyRenderers[i];
            if (s == null || s.renderer == null) continue;

            var mats = s.materialRefs;
            var templates = s.templates;

            if (mats == null || templates == null) continue;
            if (mats.Length != templates.Length) continue;

            s.renderer.sharedMaterials = mats;

            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null || templates[m] == null) continue;
                mats[m].CopyPropertiesFromMaterial(templates[m]);
            }
        }

        DynamicGI.UpdateEnvironment();

        if (logOnRestore)
            Debug.Log("[MenuVisualSnapshot] Restored menu visuals.");
    }

    static bool MatchesAnyKeyword(string text, string[] keywords)
    {
        if (string.IsNullOrEmpty(text) || keywords == null) return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string k = keywords[i];
            if (string.IsNullOrEmpty(k)) continue;

            if (text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
