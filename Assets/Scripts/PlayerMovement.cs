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
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (!mouse.rightButton.wasPressedThisFrame) return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, clickMask, QueryTriggerInteraction.Ignore))
        {
            clickTarget = hit.point;
            hasClickTarget = true;
            return;
        }

        int navMask = NavMesh.AllAreas;
        if (NavMesh.SamplePosition(ray.origin + ray.direction * 40f, out NavMeshHit navHit, 20f, navMask))
        {
            clickTarget = navHit.position;
            hasClickTarget = true;
        }
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
