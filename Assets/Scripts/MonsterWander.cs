using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterWander : MonoBehaviour
{
    public float minWait = 0.4f;
    public float maxWait = 1.6f;

    public float minWanderRadius = 6f;
    public float maxWanderRadius = 18f;

    [Header("Chase Player")]
    public float chaseRange = 6f;
    public float loseRange = 9f;
    public float minimumEffectiveChaseRange = 10f;
    public float minimumEffectiveLoseRange = 14f;
    public float chaseRepathSeconds = 0.25f;
    public bool stopChasingInSafeZone = true;
    public float playerSeparationDistance = 0.2f;

    [Header("Avoid Lit area")]
    public string litAreaName = "Lit";
    public float sampleRadius = 2f;
    public int attempts = 25;

    NavMeshAgent agent;
    int darkAreaMask;
    float nextTime;
    float nextChaseTime;
    bool isChasing;
    Transform playerTransform;

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
        if (!agent.isOnNavMesh) return;

        if (SafeZoneRegistry.IsPositionSafe(transform.position))
        {
            PickNewDestination();
            return;
        }

        if (TryGetPlayer(out Vector3 playerPos) && ShouldChasePlayer(playerPos))
        {
            ChasePlayer(playerPos);
            return;
        }

        if (isChasing)
        {
            isChasing = false;
            nextTime = Time.time + Random.Range(minWait, maxWait);
        }

        if (Time.time < nextTime) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            PickNewDestination();
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
                if (SafeZoneRegistry.IsPositionSafe(hit.position)) continue;
                agent.SetDestination(hit.position);
                nextTime = Time.time + Random.Range(minWait, maxWait);
                return;
            }
        }

        nextTime = Time.time + Random.Range(minWait, maxWait);
    }

    bool TryGetPlayer(out Vector3 position)
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform != null)
        {
            position = playerTransform.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    bool ShouldChasePlayer(Vector3 playerPos)
    {
        if (stopChasingInSafeZone && SafeZoneRegistry.IsPositionSafe(playerPos)) return false;

        float dist = Vector3.Distance(transform.position, playerPos);
        float effectiveChaseRange = Mathf.Max(chaseRange, minimumEffectiveChaseRange);
        float effectiveLoseRange = Mathf.Max(loseRange, minimumEffectiveLoseRange, effectiveChaseRange);

        if (isChasing)
            return dist <= effectiveLoseRange;

        return dist <= effectiveChaseRange;
    }

    void ChasePlayer(Vector3 playerPos)
    {
        if (!isChasing) isChasing = true;

        if (Time.time < nextChaseTime) return;

        float stopDistance = playerSeparationDistance;
        stopDistance = Mathf.Max(0.1f, stopDistance);

        agent.stoppingDistance = stopDistance;

        Vector3 toPlayer = playerPos - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= stopDistance * stopDistance)
        {
            agent.ResetPath();
            nextChaseTime = Time.time + Mathf.Max(0.05f, chaseRepathSeconds);
            return;
        }

        Vector3 desiredDestination = playerPos - toPlayer.normalized * stopDistance;
        if (!TrySetChaseDestination(desiredDestination, playerPos))
            agent.ResetPath();

        nextChaseTime = Time.time + Mathf.Max(0.05f, chaseRepathSeconds);
    }

    bool TrySetChaseDestination(Vector3 desiredDestination, Vector3 playerPos)
    {
        if (NavMesh.SamplePosition(desiredDestination, out NavMeshHit preferredHit, sampleRadius * 2f, darkAreaMask))
            return agent.SetDestination(preferredHit.position);

        if (NavMesh.SamplePosition(playerPos, out NavMeshHit playerHit, sampleRadius * 4f, darkAreaMask))
            return agent.SetDestination(playerHit.position);

        if (agent.hasPath) return true;

        return false;
    }
}
