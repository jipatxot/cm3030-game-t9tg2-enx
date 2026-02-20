using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterWander : MonoBehaviour
{
    [Header("Wander")]
    public float minWait = 0.4f;
    public float maxWait = 1.6f;
    public float minWanderRadius = 6f;
    public float maxWanderRadius = 18f;

    [Header("Chase Player")]
    public float chaseRange = 8f;
    public float loseRange = 12f;
    public float chaseRepathSeconds = 0.2f;
    public bool stopChasingInSafeZone = true;

    [Header("Prediction")]
    public bool usePrediction = true;
    public float predictTime = 0.35f;
    public float maxPredictDistance = 2f;

    [Header("Avoid Lit area")]
    public string litAreaName = "Lit";
    public float sampleRadius = 2f;
    public int attempts = 25;

    NavMeshAgent agent;
    int darkAreaMask;

    float nextTime;
    float nextChaseTime;
    float nextReacquireTime;

    bool isChasing;

    Transform playerTransform;
    CharacterController playerCC;

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
        if (!agent || !agent.enabled || !agent.isOnNavMesh)
            return;

        ReacquirePlayer();

        if (SafeZoneRegistry.IsPositionSafe(transform.position))
        {
            PickNewDestination();
            return;
        }

        if (TryGetPlayer(out Vector3 playerPos) && ShouldChasePlayer(playerPos))
        {
            SmartChase(playerPos);
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

    void ReacquirePlayer()
    {
        if (Time.time < nextReacquireTime) return;
        nextReacquireTime = Time.time + 0.5f;

        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                playerTransform = player.transform;
                playerCC = player.GetComponent<CharacterController>();
            }
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
        if (stopChasingInSafeZone && SafeZoneRegistry.IsPositionSafe(playerPos))
            return false;

        float dist = Vector3.Distance(transform.position, playerPos);

        if (isChasing)
            return dist <= loseRange;

        return dist <= chaseRange;
    }

    void SmartChase(Vector3 playerPos)
    {
        if (!isChasing) isChasing = true;

        if (Time.time < nextChaseTime) return;

        float playerRadius = 0.35f;
        if (playerCC != null)
            playerRadius = playerCC.radius;

        float stopDistance = Mathf.Max(0.05f, agent.radius + playerRadius);
        agent.stoppingDistance = stopDistance;

        Vector3 target = playerPos;

        if (usePrediction && playerCC != null)
        {
            Vector3 vel = playerCC.velocity;
            vel.y = 0f;

            Vector3 predicted = playerPos + vel * predictTime;

            Vector3 delta = predicted - playerPos;
            if (delta.magnitude > maxPredictDistance)
                predicted = playerPos + delta.normalized * maxPredictDistance;

            target = predicted;
        }

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= stopDistance * stopDistance)
        {
            agent.ResetPath();
            nextChaseTime = Time.time + chaseRepathSeconds;
            return;
        }

        Vector3 destination = target - toTarget.normalized * stopDistance;
        agent.SetDestination(destination);

        nextChaseTime = Time.time + chaseRepathSeconds;
    }
}