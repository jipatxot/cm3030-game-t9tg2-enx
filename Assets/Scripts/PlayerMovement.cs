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

    [Header("Gravity / Grounding")]
    public float gravity = 25f;
    public float groundedStick = 2f;
    public float groundCheckDistance = 0.35f;

    [Header("Controller anti-step")]
    public float stepOffset = 0.15f;
    public float slopeLimit = 45f;

    CharacterController controller;
    Camera mainCamera;
    Vector3 clickTarget;
    bool hasClickTarget;

    float verticalVel;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        // Reduce unintended climbing
        controller.stepOffset = stepOffset;
        controller.slopeLimit = slopeLimit;
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

        // Grounding and gravity
        bool grounded = IsGrounded();
        if (grounded && verticalVel < 0f)
            verticalVel = -groundedStick;

        verticalVel -= gravity * Time.deltaTime;

        Vector3 horizontal = Vector3.zero;

        if (move.sqrMagnitude >= 0.0001f)
        {
            if (move.sqrMagnitude > 1f) move.Normalize();
            horizontal = move * moveSpeed;

            Quaternion target = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        Vector3 velocity = horizontal + Vector3.up * verticalVel;
        controller.Move(velocity * Time.deltaTime);
    }

    bool IsGrounded()
    {
        if (controller.isGrounded) return true;

        Vector3 origin = transform.position + Vector3.up * 0.05f;
        return Physics.SphereCast(
            origin,
            controller.radius * 0.9f,
            Vector3.down,
            out _,
            groundCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );
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
