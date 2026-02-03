using UnityEngine;
using UnityEngine.InputSystem;

public class IsoCityCamera : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public Transform focus;
    public Renderer groundRenderer;

    [Header("Angles")]
    public float pitch = 45f;     // X rotation
    public float yaw = 45f;       // Y rotation

    [Header("Ortho zoom")]
    public float zoomSpeed = 20f;
    public float minOrtho = 2f;
    public float maxOrtho = 7f;

    [Header("Pan")]
    public float panSpeed = 25f;
    public float edgePx = 20f;
    public bool edgePanEnabled = true;

    [Header("Clamp to ground")]
    public bool clampToGround = true;
    public float clampPadding = 2f;

    [Header("Focus offset")]
    public Vector3 focusOffset = Vector3.zero;

    void Reset()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam != null) cam.orthographic = true;

        ApplyRotation();
    }

    void Update()
    {
        if (cam == null) return;

        ApplyRotation();

        if (edgePanEnabled)
            EdgePan();

        ScrollZoom();

        if (clampToGround)
            ClampToGroundBounds();
    }

    void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void EdgePan()
    {
        // Mouse position in screen pixels
        Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        float dx = 0f;
        float dz = 0f;

        if (mp.x <= edgePx) dx = -1f;
        else if (mp.x >= Screen.width - edgePx) dx = 1f;

        if (mp.y <= edgePx) dz = -1f;
        else if (mp.y >= Screen.height - edgePx) dz = 1f;

        if (dx == 0f && dz == 0f) return;

        // Move in camera-planar directions (ignore vertical)
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 move = (right * dx + forward * dz) * (panSpeed * Time.deltaTime);

        if (focus != null)
            focus.position += move;
        else
            transform.position += move;

        SyncToFocus();
    }

    void ScrollZoom()
    {
        if (Mouse.current == null) return;

        // New Input System scroll is a Vector2, y is wheel direction
        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) < 0.01f) return;

        float target = cam.orthographicSize - scrollY * (zoomSpeed * 0.01f);
        cam.orthographicSize = Mathf.Clamp(target, minOrtho, maxOrtho);
    }

    void SyncToFocus()
    {
        if (focus == null) return;

        // Place rig at focus + offset, keeping existing height
        Vector3 p = transform.position;
        Vector3 f = focus.position + focusOffset;
        transform.position = new Vector3(f.x, p.y, f.z);
    }

    void ClampToGroundBounds()
    {
        if (groundRenderer == null || focus == null) return;

        Bounds b = groundRenderer.bounds;

        float minX = b.min.x + clampPadding;
        float maxX = b.max.x - clampPadding;
        float minZ = b.min.z + clampPadding;
        float maxZ = b.max.z - clampPadding;

        Vector3 f = focus.position;
        f.x = Mathf.Clamp(f.x, minX, maxX);
        f.z = Mathf.Clamp(f.z, minZ, maxZ);
        focus.position = f;

        SyncToFocus();
    }
}
