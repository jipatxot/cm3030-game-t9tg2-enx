using UnityEngine;

public class PlayerRuntimeSetup : MonoBehaviour
{
    [Header("Material Compatibility")]
    public bool autoFixRenderShaders = true;

    [Tooltip("First-choice shader in URP projects.")]
    public string urpLitShaderName = "Universal Render Pipeline/Lit";

    [Tooltip("Fallback for non-URP projects.")]
    public string standardShaderName = "Standard";

    [Header("Animator")]
    public bool forceDisableRootMotion = true;
    public bool logMissingAnimatorController = true;

    bool configured;

    void Awake()
    {
        ConfigureNow();
    }

    void OnEnable()
    {
        ConfigureNow();
    }

    public void ConfigureNow()
    {
        if (configured) return;

        if (autoFixRenderShaders)
            FixRendererShaders();

        SetupAnimator();

        configured = true;
    }

    void SetupAnimator()
    {
        var animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        if (forceDisableRootMotion)
            animator.applyRootMotion = false;

        if (logMissingAnimatorController && animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerRuntimeSetup: Animator has no RuntimeAnimatorController. The player will not play run/walk animations until a controller is assigned.");
        }
    }

    void FixRendererShaders()
    {
        Shader target = Shader.Find(urpLitShaderName);
        if (target == null)
            target = Shader.Find(standardShaderName);
        if (target == null)
            return;

        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mats = r.sharedMaterials;
            bool changed = false;

            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;
                if (mat.shader == target) continue;

                Texture mainTex = null;
                Color color = Color.white;

                if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                if (mat.HasProperty("_Color")) color = mat.GetColor("_Color");

                mat.shader = target;

                if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                if (mainTex != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

                changed = true;
            }

            if (changed)
                r.sharedMaterials = mats;
        }
    }
}
