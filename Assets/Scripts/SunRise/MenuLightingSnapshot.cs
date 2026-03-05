using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


/// Captures the "Main Menu" lighting / sky settings so we can restore them after a sunrise win sequence.
/// 
/// Attach this to an object that is active when the game first shows the Main Menu
/// (e.g., your StartPanel root, or a persistent GameObject in the scene).
/// 
/// It snapshots:
/// - RenderSettings ambient & fog & skybox
/// - All Directional Lights currently in the scene (intensity, color, enabled)
/// 
/// Then you can restore everything later by calling Restore().
public class MenuLightingSnapshot : MonoBehaviour
{
    public static MenuLightingSnapshot Instance { get; private set; }

    [Header("Capture")]
    [Tooltip("Capture in Awake (recommended).")]
    public bool captureOnAwake = true;

    [Tooltip("If true, keep the singleton alive across scene loads.")]
    public bool dontDestroyOnLoad = false;

    [Header("Debug")]
    public bool logOnCapture = false;
    public bool logOnRestore = false;

    bool _captured;

    // RenderSettings snapshot
    AmbientMode _ambientMode;
    Color _ambientLight;
    float _ambientIntensity;

    bool _fog;
    Color _fogColor;
    float _fogDensity;
    FogMode _fogMode;

    Material _skybox;

    // Directional lights snapshot
    [Serializable]
    class DirLightState
    {
        public Light light;
        public bool enabled;
        public float intensity;
        public Color color;
    }
    readonly List<DirLightState> _dirLights = new List<DirLightState>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

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

        _skybox = RenderSettings.skybox;

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
                color = l.color
            });
        }

        _captured = true;

        if (logOnCapture)
            Debug.Log($"[MenuLightingSnapshot] Captured menu lighting. DirLights={_dirLights.Count}");
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

        RenderSettings.skybox = _skybox;

        for (int i = 0; i < _dirLights.Count; i++)
        {
            var s = _dirLights[i];
            if (s == null || s.light == null) continue;

            s.light.enabled = s.enabled;
            s.light.intensity = s.intensity;
            s.light.color = s.color;
        }

        // Update environment probes / skybox lighting
        DynamicGI.UpdateEnvironment();

        if (logOnRestore)
            Debug.Log("[MenuLightingSnapshot] Restored menu lighting.");
    }
}
