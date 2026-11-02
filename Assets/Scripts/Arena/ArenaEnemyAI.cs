using UnityEngine;
using UnityEngine.AI;

/// <summary>Single NavMeshAgent authority for hull movement/rotation; selects weighted actions.</summary>
[RequireComponent(typeof(NavMeshAgent), typeof(ArenaEnemySensor))]
public sealed class ArenaEnemyAI : MonoBehaviour
{
    [Header("Decision weights")]
    [SerializeField] private int searchWeight = 40, pursueWeight = 60, attackWeight = 100;
    [SerializeField, Min(0.02f)] private float decisionInterval = 0.2f;
    [Header("Ranges and movement")]
    [SerializeField, Min(0.1f)] private float attackRange = 18f;
    [SerializeField, Min(1f)] private float attackExitMultiplier = 1.2f;
    [SerializeField, Min(0.1f)] private float patrolRadius = 8f;
    [SerializeField, Min(0f)] private float searchDuration = 3f;
    [SerializeField] private float patrolSpeed = 2f, pursueSpeed = 4f;
    [SerializeField, Min(0.05f)] private float pathRefresh = 0.25f;
    [SerializeField] private ArenaEnemyWeapon weapon;
    private NavMeshAgent agent;
    private ArenaEnemySensor sensor;
    private ArenaEnemyAction action;
    private Vector3 spawnPosition;
    private float decisionTimer, pathTimer, searchTimer;
    private bool attackRangeLatched;
    private Transform target;
    public ArenaEnemyAction CurrentAction => action;
    public void SetTarget(Transform value) { target = value; if (sensor != null) sensor.SetTarget(value); }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>(); sensor = GetComponent<ArenaEnemySensor>();
        if (weapon == null) weapon = GetComponent<ArenaEnemyWeapon>();
        agent.updatePosition = true; agent.updateRotation = true;
        spawnPosition = transform.position;
    }
    private void Start() { ChangeAction(ArenaEnemyAction.Patrol); }
    private void Update()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { if (weapon != null) weapon.SetCanFire(false); return; }
        ArenaEnemySnapshot facts = sensor.Observe(target);
        if (facts.target == null) { if (weapon != null) weapon.SetCanFire(false); return; }
        float enter = attackRange;
        float exit = attackRange * Mathf.Max(1f, attackExitMultiplier);
        if (facts.distance <= enter) attackRangeLatched = true;
        else if (facts.distance > exit) attackRangeLatched = false;
        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0f)
        {
            decisionTimer = decisionInterval;
            ArenaEnemyAction selected = ArenaEnemyDecision.Choose(new ArenaEnemyDecisionInput
            {
                visible = facts.visible,
                hasMemory = facts.hasLastSeenPosition,
                attackInRange = attackRangeLatched,
                previous = action
            }, searchWeight, pursueWeight, attackWeight);
            // Attack is only applicable with LOS; attack range has hysteresis, but LOS loss immediately interrupts fire.
            ChangeAction(selected);
        }
        Execute(facts);
    }
    private void ChangeAction(ArenaEnemyAction next)
    {
        if (action == next && agent != null) return;
        action = next;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        switch (action)
        {
            case ArenaEnemyAction.Patrol: agent.isStopped = false; agent.speed = patrolSpeed; PickPatrolPoint(); break;
            case ArenaEnemyAction.Pursue: agent.isStopped = false; agent.speed = pursueSpeed; pathTimer = 0f; break;
            case ArenaEnemyAction.Search: agent.isStopped = false; agent.speed = patrolSpeed; searchTimer = searchDuration; pathTimer = 0f; break;
            case ArenaEnemyAction.Attack: agent.isStopped = true; agent.ResetPath(); break;
        }
    }
    private void Execute(ArenaEnemySnapshot facts)
    {
        switch (action)
        {
            case ArenaEnemyAction.Patrol:
                if (Arrived()) PickPatrolPoint();
                break;
            case ArenaEnemyAction.Pursue:
                UpdateDestination(facts.target.position);
                break;
            case ArenaEnemyAction.Search:
                searchTimer -= Time.deltaTime;
                if (searchTimer <= 0f) { sensor.InvalidateMemory(); ChangeAction(ArenaEnemyAction.Patrol); break; }
                if (facts.hasLastSeenPosition) UpdateDestination(facts.lastSeenPosition);
                // The hull remains under NavMeshAgent rotation authority while searching.
                break;
            case ArenaEnemyAction.Attack:
                if (!facts.visible) { if (weapon != null) weapon.SetCanFire(false); break; }
                agent.isStopped = true;
                if (weapon != null) { weapon.SetTarget(facts.target); weapon.SetCanFire(true); }
                break;
        }
        if (action != ArenaEnemyAction.Attack && weapon != null) weapon.SetCanFire(false);
    }
    private void UpdateDestination(Vector3 point) { pathTimer -= Time.deltaTime; if (pathTimer <= 0f) { pathTimer = pathRefresh; agent.SetDestination(point); } }
    private bool Arrived() => !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f);
    private void PickPatrolPoint()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        Vector3 candidate = spawnPosition + Random.insideUnitSphere * patrolRadius;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }
}
