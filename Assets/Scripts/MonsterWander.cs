using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterWander : MonoBehaviour
{
    public float minWait = 0.4f;
    public float maxWait = 1.6f;

    public float minWanderRadius = 6f;
    public float maxWanderRadius = 18f;

    [Header("Avoid Lit area")]
    public string litAreaName = "Lit";
    public float sampleRadius = 2f;
    public int attempts = 25;

    NavMeshAgent agent;
    int darkAreaMask;
    float nextTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        int lit = NavMesh.GetAreaFromName(litAreaName);
        int litMask = (lit < 0) ? 0 : (1 << lit);
        darkAreaMask = NavMesh.AllAreas & ~litMask;

        agent.areaMask = darkAreaMask;
    }

    void Update()
    {
        if (agent == null || !agent.enabled) return;
        if (!agent.isOnNavMesh) return; // <- key line

        if (Time.time < nextTime) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            PickNewDestination();
        }
    }

    void PickNewDestination()
    {
        float r = Random.Range(minWanderRadius, maxWanderRadius);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 circle = Random.insideUnitCircle * r;
            Vector3 guess = transform.position + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(guess, out NavMeshHit hit, sampleRadius, darkAreaMask))
            {
                agent.SetDestination(hit.position);
                nextTime = Time.time + Random.Range(minWait, maxWait);
                return;
            }
        }

        nextTime = Time.time + Random.Range(minWait, maxWait);
    }
}