using UnityEngine;

public class PlayerAnimationDriver : MonoBehaviour
{
    public Animator animator;
    public CharacterController controller;
    public float dampTime = 0.1f;

    void Awake()
    {
        if (!controller)
            controller = GetComponent<CharacterController>();

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (!animator || !controller)
            return;

        Vector3 velocity = controller.velocity;
        velocity.y = 0f; // ignore vertical motion

        animator.SetFloat("Speed", velocity.magnitude, dampTime, Time.deltaTime);
    }
}