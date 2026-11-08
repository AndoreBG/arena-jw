using UnityEngine;
using UnityEngine.AI;

/// <summary>Estados da nave alienígena.</summary>
public enum AlienShipState { Patrol, Pursue, Attack, Search, Disabled }

/// <summary>
/// IA da nave: três estados ativos (Patrol, Pursue, Attack) escolhidos por pesos estáticos
/// (40 / 60 / 100) e reavaliados a cada <c>decisionInterval</c>.
///
/// Autoridade de movimento:
///  - NavMeshAgent manda no X/Z, sem rotação (<c>updateRotation = false</c>, <c>updateUpAxis = false</c>).
///  - A rotação do eixo Y é do <see cref="AlienShipWeapon"/> (a nave encara o jogador para atirar).
///  - A altura de voo é do <see cref="EnemyHover"/>.
///
/// O sensor (<see cref="ArenaEnemySensor"/>) só informa fatos; nenhuma decisão depende dele.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[AddComponentMenu("Arena/Enemy/Alien Ship AI")]
public sealed class AlienShipAI : MonoBehaviour, IEnemyStateProvider
{
    [Header("Alvos")]
    [SerializeField] private Transform target;
    [SerializeField] private ArenaEnemySensor sensor;

    [Header("Decisão (pesos estáticos)")]
    [SerializeField, Min(0)] private int patrolWeight = 40;
    [SerializeField, Min(0)] private int pursueWeight = 60;
    [SerializeField, Min(0)] private int attackWeight = 100;
    [SerializeField, Min(0.02f)] private float decisionInterval = 0.2f;

    [Header("Patrulha")]
    [Tooltip("Pausa parada antes de escolher um novo ponto, em segundos (1,5 a 4 s).")]
    [SerializeField] private Vector2 patrolIdleTime = new Vector2(1.5f, 4f);
    [SerializeField, Min(1f)] private float patrolRadius = 12f;
    [SerializeField, Min(0.05f)] private float patrolSpeed = 2f;
    [SerializeField] private float patrolArriveDistance = 1.5f;

    [Header("Perseguição")]
    [SerializeField, Min(0.05f)] private float pursueSpeed = 4f;
    [SerializeField, Min(0.05f)] private float pursueRefresh = 0.25f;

    [Header("Ataque")]
    [Tooltip("Distância horizontal em que a nave para e começa a atirar.")]
    [SerializeField, Min(1f)] private float attackRange = 18f;
    [Tooltip("Multiplicador de saída do ataque (histerese).")]
    [SerializeField, Min(1f)] private float attackExitMultiplier = 1.2f;

    [Header("Agressividade (dificuldade)")]
    [Tooltip("0 = IA fraca (menos perseguição, alcance curto). 1 = IA forte.")]
    [SerializeField, Range(0f, 1f)] private float aggression = 0.5f;

    private NavMeshAgent agent;
    private AlienShipWeapon weapon;
    private AlienShipState state = AlienShipState.Patrol;
    private Vector3 spawnPosition;
    private float decisionTimer;
    private float pathTimer;
    private float idleTimer;
    private bool attackRangeLatched;
    private bool hasTarget;
    private bool waitingForPatrol;
    private bool aware;

    public AlienShipState State => state;
    public string StateName => state.ToString();

    /// <summary>O alvo está dentro do alcance de percepção (visão ou proximidade).</summary>
    public bool IsAwareOfTarget => aware;
    public Transform Target => target;
    public bool HasTarget => hasTarget && target != null;

    /// <summary>Peso do estado na decisão (100 para ataque, 60 perseguição, 40 patrulha).</summary>
    public int StateWeight => state switch
    {
        AlienShipState.Attack => attackWeight,
        AlienShipState.Pursue => pursueWeight,
        AlienShipState.Patrol => patrolWeight,
        _ => 0
    };

    public float Aggression
    {
        get => aggression;
        set => aggression = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (sensor == null) sensor = GetComponent<ArenaEnemySensor>();
        if (weapon == null) weapon = GetComponent<AlienShipWeapon>();
        if (weapon == null) weapon = GetComponentInChildren<AlienShipWeapon>();

        spawnPosition = transform.position;
        if (target != null) SetTarget(target);

        // A nave voa: o agente manda no X/Z, a rotação Y é da arma e a altura é do EnemyHover
        // (via baseOffset). Não desligamos updatePosition — isso tiraria o agente do NavMesh.
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.autoBraking = false;
    }

    private void Start() => EnterState(AlienShipState.Patrol);

    /// <summary>
    /// Define o alvo. Sem alvo a nave patrulha, mas ela nunca "esquece" um alvo já atribuído:
    /// pode receber null antes de o spawner ligá-la, e o SetTarget do spawner vem logo depois.
    /// </summary>
    public void SetTarget(Transform value)
    {
        if (value != null) target = value;
        if (sensor != null) sensor.SetTarget(target);
        if (weapon != null) weapon.SetTarget(target);
    }

    private void Update()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            SetWeaponActive(false);
            return;
        }

        hasTarget = target != null;
        if (!hasTarget)
        {
            SetWeaponActive(false);
            if (state != AlienShipState.Patrol) EnterState(AlienShipState.Patrol);
            else TickPatrol();
            return;
        }

        float distance = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up).magnitude;

        float enter = AttackEnterRange;
        float exit = enter * Mathf.Max(1f, attackExitMultiplier);

        if (distance <= enter) attackRangeLatched = true;
        else if (distance > exit) attackRangeLatched = false;

        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0f)
        {
            decisionTimer = decisionInterval;

            // Percepção amostrada no ritmo da decisão (0,2 s), não a cada frame: o RaycastAll
            // do sensor alocaria memória 60x por segundo em cada nave.
            // Visão (sensor) OU proximidade. Sem sensor, vale só a proximidade.
            bool visible = false;
            if (sensor != null) visible = sensor.Observe(target).visible;
            aware = visible || distance <= AwarenessRadius;

            AlienShipState next = DecideState();
            if (next != state) EnterState(next);
        }

        Execute();
    }

    /// <summary>Distância horizontal para entrar no ataque. A agressividade encurta o alcance.</summary>
    private float AttackEnterRange => Mathf.Lerp(attackRange * 0.7f, attackRange, aggression);

    /// <summary>Raio em que a nave percebe o jogador mesmo sem linha de visão direta.</summary>
    private float AwarenessRadius => attackRange * 2f;

    // ATENÇÃO ao primeiro argumento: o Choose espera (hasTarget, attackInRange, ...).
    // Passar 'aware' aqui fazia a nave voltar a patrulhar sempre que perdia a visão do
    // jogador — mesmo com ele do lado — e ela nunca chegava a entrar em Attack, estado em
    // que a arma é liberada. Era isso que impedia o inimigo de atirar.
    private AlienShipState DecideState() => AlienShipDecision.Choose(hasTarget, attackRangeLatched,
        patrolWeight, pursueWeight, attackWeight);

    private void EnterState(AlienShipState next)
    {
        state = next;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        switch (state)
        {
            case AlienShipState.Patrol:
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0f;
                agent.ResetPath();
                // Começa parada, esperando o tempo sorteado (1,5 a 4 s).
                waitingForPatrol = true;
                idleTimer = Random.Range(patrolIdleTime.x, patrolIdleTime.y);
                agent.isStopped = true;
                break;

            case AlienShipState.Pursue:
                agent.isStopped = false;
                agent.speed = Mathf.Lerp(pursueSpeed * 0.75f, pursueSpeed, aggression);
                agent.stoppingDistance = 0f;
                pathTimer = 0f;
                break;

            case AlienShipState.Attack:
                agent.isStopped = true;
                agent.ResetPath();
                break;
        }
    }

    private void Execute()
    {
        switch (state)
        {
            case AlienShipState.Patrol:
                TickPatrol();
                SetWeaponActive(false);
                break;

            case AlienShipState.Pursue:
                SetWeaponActive(false);
                pathTimer -= Time.deltaTime;
                if (pathTimer > 0f) break;

                pathTimer = pursueRefresh;

                // Sem visão, vai até a última posição vista; com visão, para um pouco antes
                // do jogador (a distância de onde o ataque começa).
                bool hasMemory = false;
                Vector3 lastSeen = target.position;
                if (sensor != null)
                {
                    ArenaEnemySnapshot facts = sensor.Snapshot;
                    hasMemory = facts.hasLastSeenPosition;
                    if (hasMemory) lastSeen = facts.lastSeenPosition;
                }

                if (!aware && hasMemory)
                {
                    agent.isStopped = false;
                    agent.SetDestination(lastSeen);
                    break;
                }

                Vector3 toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
                float stop = Mathf.Max(2f, AttackEnterRange * 0.8f);
                agent.isStopped = false;
                agent.SetDestination(toTarget.sqrMagnitude > 0.01f
                    ? target.position - toTarget.normalized * stop
                    : target.position);
                break;

            case AlienShipState.Attack:
                SetWeaponActive(true);
                break;
        }
    }

    /// <summary>
    /// Patrulha em dois tempos: (1) parada esperando o tempo sorteado e (2) indo até um ponto
    /// navegável. Sem separar os dois, definir um destino e parar o agente no mesmo frame
    /// travava a nave no lugar.
    /// </summary>
    private void TickPatrol()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        if (waitingForPatrol)
        {
            agent.isStopped = true;
            idleTimer -= Time.deltaTime;
            if (idleTimer > 0f) return;

            waitingForPatrol = false;
            PickPatrolPoint();
            return;
        }

        bool arrived = !agent.pathPending &&
                       (!agent.hasPath || agent.remainingDistance <= patrolArriveDistance);

        if (arrived)
        {
            waitingForPatrol = true;
            agent.isStopped = true;
            idleTimer = Random.Range(patrolIdleTime.x, patrolIdleTime.y);
        }
    }

    private void PickPatrolPoint()
    {
        Vector3 origin = spawnPosition == Vector3.zero || Random.value < 0.5f ? transform.position : spawnPosition;
        float radius = Mathf.Lerp(patrolRadius * 0.6f, patrolRadius, aggression);
        Vector3 candidate = origin + Random.insideUnitSphere * radius;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius * 1.5f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
            return;
        }

        // Nenhum ponto válido: espera de novo, sem parar para sempre.
        waitingForPatrol = true;
        agent.isStopped = true;
        idleTimer = Random.Range(patrolIdleTime.x, patrolIdleTime.y) * 0.5f;
    }

    private void SetWeaponActive(bool value)
    {
        if (weapon == null) weapon = GetComponentInChildren<AlienShipWeapon>();
        if (weapon != null) weapon.SetCanFire(value);
    }

    private void OnDisable() => SetWeaponActive(false);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}


/// <summary>
/// Regra de decisão da nave, isolada para teste: só entram na disputa os estados aplicáveis
/// agora e o maior peso vence (empate fica com o estado mais agressivo).
/// </summary>
public static class AlienShipDecision
{
    public static AlienShipState Choose(bool hasTarget, bool attackInRange,
        int patrolWeight, int pursueWeight, int attackWeight)
    {
        AlienShipState best = AlienShipState.Patrol;
        int bestWeight = patrolWeight;

        // Sem alvo, patrulhar é a única opção.
        if (!hasTarget) return AlienShipState.Patrol;

        if (pursueWeight >= bestWeight) { best = AlienShipState.Pursue; bestWeight = pursueWeight; }

        bool canAttack = attackInRange;
        if (canAttack && attackWeight >= bestWeight) { best = AlienShipState.Attack; bestWeight = attackWeight; }

        return best;
    }
}
