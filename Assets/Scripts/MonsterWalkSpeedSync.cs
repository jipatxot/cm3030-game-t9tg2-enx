using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterWalkSpeedSync : MonoBehaviour
{
    public float speedMultiplier = 1f;
    public float damp = 0.12f;

    NavMeshAgent agent;
    Animator[] anims;
    float current;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Get animators on LOD children
        anims = GetComponentsInChildren<Animator>(true);

        // Make sure no animator uses root motion
        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i]) anims[i].applyRootMotion = false;
        }

        current = 1f;
    }

    void Update()
    {
        if (agent == null) return;
        if (anims == null || anims.Length == 0) return;

        float v = agent.velocity.magnitude;
        float t = (agent.speed > 0.01f) ? (v / agent.speed) : 0f;

        float target = Mathf.Max(0.1f, t * speedMultiplier);
        current = Mathf.Lerp(current, target, damp);

        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i]) anims[i].speed = current;
        }
    }
}
