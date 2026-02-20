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

    [Header("Focus offset (general)")]
    public Vector3 focusOffset = Vector3.zero;

    [Header("Follow Player")]
    public bool snapToPlayerOnMoveInput = true;
    public string playerTag = "Player";
    public float followSmooth = 18f;

    [Header("Player centering when snapped")]
    [Tooltip("X moves along camera right, Y moves along camera forward (planar). Default Y = -5.")]
    public Vector2 snapCenterOffset = new Vector2(0f, -5f);

    [Header("Mouse Orbit")]
    public bool orbitWithRightMouse = true;
    public float orbitSensitivity = 0.12f;
    public float minPitch = 25f;
    public float maxPitch = 70f;

    Transform player;

    void Reset()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam != null) cam.orthographic = true;

        EnsureFocusExists();
        ApplyRotation();
        SyncToFocus();
    }

    void Update()
    {
        if (cam == null) return;

        EnsureFocusExists();
        EnsurePlayer();

        bool orbiting = IsOrbiting();

        if (orbitWithRightMouse)
            MouseOrbit();

        ApplyRotation();

        bool hasMoveInput = HasPlayerMoveInput();

        // Free camera when player not moving
        // Stop edge pan while orbiting to avoid weird combined motion
        if (edgePanEnabled && !orbiting)
            EdgePan();

        // Snap back onto player when moving
        if (hasMoveInput && player != null)
            FollowPlayerOnMoveInput();

        ScrollZoom();

        if (clampToGround)
            ClampToGroundBounds();
    }

    bool IsOrbiting()
    {
        var mouse = Mouse.current;
        return mouse != null && mouse.rightButton.isPressed;
    }

    void EnsureFocusExists()
    {
        if (focus != null) return;

        var go = new GameObject("CameraFocus");
        go.transform.SetParent(null);
        go.transform.position = transform.position;
        focus = go.transform;
    }

    void EnsurePlayer()
    {
        if (player != null) return;

        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    bool HasPlayerMoveInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return false;

        return kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed
            || kb.upArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.downArrowKey.isPressed || kb.rightArrowKey.isPressed;
    }

    void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void MouseOrbit()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.rightButton.isPressed) return;

        Vector2 d = mouse.delta.ReadValue();
        yaw += d.x * orbitSensitivity;
        pitch -= d.y * orbitSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void EdgePan()
    {
        Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        float dx = 0f;
        float dz = 0f;

        if (mp.x <= edgePx) dx = -1f;
        else if (mp.x >= Screen.width - edgePx) dx = 1f;

        if (mp.y <= edgePx) dz = -1f;
        else if (mp.y >= Screen.height - edgePx) dz = 1f;

        if (dx == 0f && dz == 0f) return;

        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 move = (right * dx + forward * dz) * (panSpeed * Time.deltaTime);

        if (focus != null)
            focus.position += move;
        else
            transform.position += move;

        SyncToFocus();
    }

    void FollowPlayerOnMoveInput()
    {
        if (focus == null || player == null) return;

        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 centerOffsetWorld = right * snapCenterOffset.x + forward * snapCenterOffset.y;
        Vector3 target = player.position + centerOffsetWorld;

        if (snapToPlayerOnMoveInput)
            focus.position = target;
        else
            focus.position = Vector3.Lerp(focus.position, target, followSmooth * Time.deltaTime);

        SyncToFocus();
    }

    void ScrollZoom()
    {
        if (Mouse.current == null) return;

        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) < 0.01f) return;

        float target = cam.orthographicSize - scrollY * (zoomSpeed * 0.01f);
        cam.orthographicSize = Mathf.Clamp(target, minOrtho, maxOrtho);
    }

    void SyncToFocus()
    {
        if (focus == null) return;

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
