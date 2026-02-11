using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 4.5f;
    public float rotationSpeed = 540f;
    public float clickStopDistance = 0.25f;
    public LayerMask clickMask = ~0;

    [Header("Click-to-move")]
    public float maxClickRayDistance = 1000f;
    public float navMeshSampleRadius = 3f;

    CharacterController controller;
    Camera mainCamera;
    Vector3 clickTarget;
    bool hasClickTarget;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;
    }

    void Update()
    {
        HandleRightClickMoveTarget();

        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude > 0.0001f)
            hasClickTarget = false;

        Vector3 move = new Vector3(input.x, 0f, input.y);

        if (move.sqrMagnitude < 0.0001f && hasClickTarget)
            move = GetClickMoveVector();

        if (move.sqrMagnitude < 0.0001f) return;

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        Vector3 desired = move * (moveSpeed * Time.deltaTime);
        controller.Move(desired);

        Quaternion target = Quaternion.LookRotation(move, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    void HandleRightClickMoveTarget()
    {
        if (!WasRightClickPressedThisFrame())
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(GetPointerScreenPosition());

        if (TryGetClickedWorldPoint(ray, out Vector3 worldPoint) && TryGetNavMeshPoint(worldPoint, out Vector3 navPoint))
        {
            clickTarget = navPoint;
            hasClickTarget = true;
        }
    }

    bool WasRightClickPressedThisFrame()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            return true;

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    Vector2 GetPointerScreenPosition()
    {
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    bool TryGetClickedWorldPoint(Ray ray, out Vector3 point)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, maxClickRayDistance, clickMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        Plane movePlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (movePlane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    bool TryGetNavMeshPoint(Vector3 desiredPoint, out Vector3 navPoint)
    {
        if (NavMesh.SamplePosition(desiredPoint, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            navPoint = navHit.position;
            return true;
        }

        navPoint = Vector3.zero;
        return false;
    }

    Vector3 GetClickMoveVector()
    {
        Vector3 toTarget = clickTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= clickStopDistance * clickStopDistance)
        {
            hasClickTarget = false;
            return Vector3.zero;
        }

        return toTarget.normalized;
    }

    Vector2 ReadMoveInput()
    {
        float x = 0f;
        float y = 0f;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (x == 0f)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        }

        if (y == 0f)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
        }
#endif

        return new Vector2(x, y);
    }
}
