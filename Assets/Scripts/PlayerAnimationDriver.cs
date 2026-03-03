using UnityEngine;

public class PlayerAnimationDriver : MonoBehaviour
{
    public Animator animator;
    public CharacterController controller;

    [Header("Smoothing")]
    public float dampUp = 0.1f;     // when starting to move
    public float dampDown = 0.02f;  // when stopping

    float currentSpeed;

    void Awake()
    {
        if (!controller) controller = GetComponent<CharacterController>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (!animator || !controller) return;

        Vector3 v = controller.velocity;
        v.y = 0f;

        float target = v.magnitude;

        float damp = (target > currentSpeed) ? dampUp : dampDown;

        animator.SetFloat("Speed", target, damp, Time.deltaTime);

        currentSpeed = animator.GetFloat("Speed");
    }
}