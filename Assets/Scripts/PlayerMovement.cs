using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
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
        if (!WasRightClickPressed(out Vector2 screenPos)) return;

        if (IsPointerOverUi()) return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (TryGetClickedWorldPoint(ray, out Vector3 worldPoint) && TryGetNavMeshPoint(worldPoint, out Vector3 navPoint))
        {
            clickTarget = navPoint;
            hasClickTarget = true;
        }
    }

    bool WasRightClickPressed(out Vector2 screenPos)
    {
        screenPos = Vector2.zero;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (!mouse.rightButton.wasPressedThisFrame) return false;
            screenPos = mouse.position.ReadValue();
            return true;
        }

        if (!Input.GetMouseButtonDown(1)) return false;

        screenPos = Input.mousePosition;
        return true;
    }

    bool IsPointerOverUi()
    {
        if (EventSystem.current == null) return false;

        var mouse = Mouse.current;
        if (mouse != null)
            return EventSystem.current.IsPointerOverGameObject(mouse.deviceId);

        return EventSystem.current.IsPointerOverGameObject();
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
        var keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;

        return new Vector2(x, y);
    }
}
