using UnityEngine;

public class SunPinnedToScreen3D : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;

    [Header("Screen Position (Viewport)")]
    [Range(0f, 1f)] public float viewportX = 0.5f;
    [Range(0f, 1f)] public float viewportY = 0.88f;

    [Header("Distance")]
    [Min(0.1f)] public float distanceFromCamera = 8f;

    [Header("Follow")]
    public bool keepFacingCamera = true;
    public Vector3 localEulerOffset = Vector3.zero;

    [Header("Scale")]
    public bool preserveInitialScale = true;

    private Vector3 _initialScale;
    private bool _loggedNoCamera;

    private void Awake()
    {
        _initialScale = transform.localScale;
        TryFindCamera();
    }

    private void OnEnable()
    {
        TryFindCamera();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            TryFindCamera();

        if (targetCamera == null)
        {
            if (!_loggedNoCamera)
            {
                Debug.LogWarning("[SunPinnedToScreen3D] No camera found. Make sure your render camera exists and has Tag = MainCamera.");
                _loggedNoCamera = true;
            }
            return;
        }

        // Fixed to screen position (Viewport coordinates)
        Vector3 worldPos = targetCamera.ViewportToWorldPoint(
            new Vector3(viewportX, viewportY, distanceFromCamera)
        );
        transform.position = worldPos;

        // Orient the sprite towards the camera (to avoid incorrect sprite angles).
        if (keepFacingCamera)
        {
            transform.rotation = targetCamera.transform.rotation * Quaternion.Euler(localEulerOffset);
        }

        if (preserveInitialScale)
        {
            transform.localScale = _initialScale;
        }
    }

    private void TryFindCamera()
    {
        // 1) Priority MainCamera
        targetCamera = Camera.main;
        if (targetCamera != null)
        {
            _loggedNoCamera = false;
            return;
        }

        // 2) Degradation: Find any camera that is currently in use.
#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var cams = Object.FindObjectsOfType<Camera>();
#endif
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
            {
                targetCamera = cams[i];
                _loggedNoCamera = false;
                return;
            }
        }
    }
}