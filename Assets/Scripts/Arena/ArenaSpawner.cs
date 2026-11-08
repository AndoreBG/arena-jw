using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Respawn infinito por pontos seguros. Conta instâncias vivas (não o total de criações) e
/// respeita o limite da dificuldade.
///
/// A dificuldade vem do <see cref="SpawnerConfig"/> (asset) ou, se ele não estiver configurado,
/// do <see cref="ArenaManager"/> (que lê a escolha do menu em PlayerPrefs).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Arena Spawner")]
public sealed class ArenaSpawner : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Configuração do spawner. Contém a dificuldade usada quando o ArenaManager não define uma.")]
    [SerializeField] private SpawnerConfig spawnerConfig;

    [Header("Prefab e pontos (referências de cena)")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Fallback (sem pontos fixos)")]
    [SerializeField] private bool useRandomNavMeshPoints = true;
    [SerializeField, Min(1f)] private float ringRadiusMin = 28f;
    [SerializeField, Min(1f)] private float ringRadiusMax = 45f;

    [Header("Segurança de spawn")]
    [SerializeField, Min(0.1f)] private float retryInterval = 3f;
    [SerializeField, Min(0f)] private float minimumPlayerDistance = 18f;
    [SerializeField] private Vector3 occupancyHalfExtents = new Vector3(1.6f, 1.2f, 2f);
    [SerializeField] private Vector3 occupancyOffset = new Vector3(0f, 3f, 0f);
    [Tooltip("Layers que bloqueiam o spawn (muros, árvores, outros inimigos). Triggers são ignorados.")]
    [SerializeField] private LayerMask occupancyMask = ~0;
    [SerializeField, Min(0.01f)] private float navMeshTolerance = 0.5f;

    private readonly HashSet<ArenaEnemyRegistration> alive = new HashSet<ArenaEnemyRegistration>();
    private ArenaManager manager;
    private Transform player;
    private float timer;
    private bool configErrorReported;

    public int AliveCount => alive.Count;
    public ArenaManager Manager => manager;

    /// <summary>
    /// Limite de inimigos vivos. A dificuldade escolhida no menu (via ArenaManager) manda;
    /// o SpawnerConfig é apenas o fallback quando não há manager.
    /// </summary>
    public int EnemyLimit
    {
        get
        {
            if (manager != null && manager.Difficulty != null) return manager.EnemyLimit;
            if (spawnerConfig != null && spawnerConfig.Difficulty != null) return spawnerConfig.MaxAliveEnemies;
            return 2;
        }
    }

    /// <summary>Intervalo entre tentativas de spawn (mesma prioridade do limite).</summary>
    public float Interval
    {
        get
        {
            if (manager != null && manager.Difficulty != null) return manager.SpawnInterval;
            if (spawnerConfig != null && spawnerConfig.Difficulty != null) return spawnerConfig.SpawnInterval;
            return retryInterval;
        }
    }

    public void SetManager(ArenaManager value)
    {
        manager = value;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;

        if (spawnerConfig != null)
        {
            minimumPlayerDistance = spawnerConfig.MinimumPlayerDistance;
            occupancyHalfExtents = spawnerConfig.OccupancyHalfExtents;
            occupancyOffset = spawnerConfig.OccupancyOffset;
            useRandomNavMeshPoints = spawnerConfig.UseRandomNavMeshPoints;
            ringRadiusMin = spawnerConfig.RingRadiusMin;
            ringRadiusMax = spawnerConfig.RingRadiusMax;
        }

        // Primeiro inimigo não espera o intervalo: a partida começa com o limite cheio.
        timer = Interval;
    }

    private void Update()
    {
        alive.RemoveWhere(x => x == null);
        if (manager != null && manager.State != ArenaRunState.Playing) return;
        if (alive.Count >= EnemyLimit) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Interval;
        TrySpawnOne();
    }

    private void TrySpawnOne()
    {
        if (!enemyPrefab)
        {
            if (!configErrorReported)
            {
                Debug.LogError("[ArenaSpawner] Prefab do inimigo não configurado.", this);
                configErrorReported = true;
            }
            return;
        }

        // Procura o jogador só até achar (o spawner pode nascer antes dele na cena).
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if (!TryFindPosition(out Vector3 position, out Quaternion rotation))
            return;

        GameObject enemy = Instantiate(enemyPrefab, position, rotation);
        enemy.name = enemyPrefab.name + "_Vivo";

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null) agent = enemy.GetComponentInChildren<NavMeshAgent>();

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            Debug.LogError("[ArenaSpawner] O prefab precisa de um NavMeshAgent válido sobre o NavMesh. " +
                           "Instância descartada.", enemy);
            Destroy(enemy);
            return;
        }

        // Scripts novos do inimigo (Assets/Scripts/Inimigo).
        // O spawner entrega o jogador e o asset de dificuldade; quem decide o resto é a máquina
        // de estados da própria nave.
        InimigoMaquinaDeEstados maquina = enemy.GetComponent<InimigoMaquinaDeEstados>();
        if (maquina != null)
        {
            maquina.DefinirJogador(player);
            maquina.DefinirConfiguracao(ResolveConfiguracaoDoInimigo());
        }

        ArenaEnemyRegistration registration = enemy.GetComponent<ArenaEnemyRegistration>();
        if (registration == null) registration = enemy.AddComponent<ArenaEnemyRegistration>();
        registration.Initialize(this, manager);
        alive.Add(registration);
    }

    /// <summary>
    /// O asset de números do inimigo para a dificuldade em uso. Fica no próprio
    /// <see cref="DifficultyConfig"/>; sem ele, cai no configuracaoPadrao do prefab.
    /// </summary>
    private ConfiguracaoDoInimigo ResolveConfiguracaoDoInimigo()
    {
        if (manager != null && manager.Difficulty != null && manager.Difficulty.Inimigo != null)
            return manager.Difficulty.Inimigo;

        if (spawnerConfig != null && spawnerConfig.Difficulty != null &&
            spawnerConfig.Difficulty.Inimigo != null)
            return spawnerConfig.Difficulty.Inimigo;

        return null;
    }

    private bool TryFindPosition(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            List<int> order = new List<int>(spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            foreach (int index in order)
            {
                Transform point = spawnPoints[index];
                if (point == null) continue;
                if (IsPositionValid(point.position, point.rotation, out position, out rotation)) return true;
            }
        }

        if (useRandomNavMeshPoints && player != null)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Random.Range(ringRadiusMin, ringRadiusMax);
                Vector3 candidate = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Quaternion facing = Quaternion.LookRotation((player.position - candidate).normalized, Vector3.up);
                if (IsPositionValid(candidate, facing, out position, out rotation)) return true;
            }
        }

        return false;
    }

    private bool IsPositionValid(Vector3 point, Quaternion pointRotation, out Vector3 position, out Quaternion rotation)
    {
        position = point;
        rotation = pointRotation;

        if (player != null && HorizontalDistance(point, player.position) < minimumPlayerDistance)
            return false;

        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, navMeshTolerance, NavMesh.AllAreas))
            return false;

        if (Vector3.Distance(hit.position, point) > navMeshTolerance)
            return false;

        Vector3 center = hit.position + pointRotation * occupancyOffset;
        if (Physics.CheckBox(center, occupancyHalfExtents, pointRotation, occupancyMask, QueryTriggerInteraction.Ignore))
            return false;

        position = hit.position;
        return true;
    }

    internal void Unregister(ArenaEnemyRegistration registration, bool killed)
    {
        if (!alive.Remove(registration)) return;
        if (killed && manager != null) manager.RegisterEnemyKill(this);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (Transform point in spawnPoints)
                if (point != null) Gizmos.DrawWireCube(point.position + occupancyOffset, occupancyHalfExtents * 2f);
        }

        if (useRandomNavMeshPoints && minimumPlayerDistance > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, ringRadiusMin);
            Gizmos.DrawWireSphere(transform.position, ringRadiusMax);
        }
    }
}
