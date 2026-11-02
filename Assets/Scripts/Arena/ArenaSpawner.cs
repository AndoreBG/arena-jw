using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Respawn infinito por pontos seguros; conta instâncias vivas, não total de criações.</summary>
public sealed class ArenaSpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(0.1f)] private float retryInterval = 3f;
    [SerializeField, Min(0f)] private float minimumPlayerDistance = 8f;
    [SerializeField] private Vector3 occupancyHalfExtents = new Vector3(1.5f, 1f, 2f);
    [SerializeField] private Vector3 occupancyOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private LayerMask occupancyMask = ~0;
    [SerializeField, Min(0.01f)] private float navMeshTolerance = 0.5f;
    private readonly HashSet<ArenaEnemyRegistration> alive = new HashSet<ArenaEnemyRegistration>();
    private ArenaManager manager;
    private Transform player;
    private float timer;
    private bool configErrorReported;
    public int AliveCount => alive.Count;
    public void SetManager(ArenaManager value)
    {
        manager = value;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
    }
    private void Update()
    {
        alive.RemoveWhere(x => x == null);
        if (manager == null || manager.State != ArenaRunState.Playing || alive.Count >= manager.EnemyLimit) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = retryInterval;
        TrySpawnOne();
    }
    private void TrySpawnOne()
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        { if (!configErrorReported) { Debug.LogError("[ArenaSpawner] Configure o prefab inimigo e pelo menos um ponto de spawn.", this); configErrorReported = true; } return; }
        List<int> order = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++) if (spawnPoints[i] != null) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
        foreach (int index in order)
        {
            Transform point = spawnPoints[index]; Vector3 pos = point.position;
            if (player != null && HorizontalDistance(pos, player.position) < minimumPlayerDistance) continue;
            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, navMeshTolerance, NavMesh.AllAreas) || Vector3.Distance(hit.position, pos) > navMeshTolerance) continue;
            Quaternion rotation = point.rotation;
            Vector3 center = hit.position + rotation * occupancyOffset;
            if (Physics.CheckBox(center, occupancyHalfExtents, rotation, occupancyMask, QueryTriggerInteraction.Ignore)) continue;
            GameObject enemy = Instantiate(enemyPrefab, hit.position, rotation);
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null) agent = enemy.GetComponentInChildren<NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            { Debug.LogError("[ArenaSpawner] Prefab precisa de NavMeshAgent válido no NavMesh; instância descartada.", enemy); Destroy(enemy); continue; }
            ArenaEnemyAI ai = enemy.GetComponent<ArenaEnemyAI>(); if (ai != null) ai.SetTarget(player);
            ArenaEnemyWeapon weapon = enemy.GetComponent<ArenaEnemyWeapon>(); if (weapon != null) weapon.SetTarget(player);
            ArenaEnemyRegistration registration = enemy.GetComponent<ArenaEnemyRegistration>();
            if (registration == null) registration = enemy.AddComponent<ArenaEnemyRegistration>();
            registration.Initialize(this, manager);
            alive.Add(registration);
            return;
        }
    }
    internal void Unregister(ArenaEnemyRegistration registration, bool killed)
    {
        if (!alive.Remove(registration)) return;
        if (killed && manager != null) manager.RegisterEnemyKill(this);
    }
    private static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (Transform point in spawnPoints) if (point != null) Gizmos.DrawWireCube(point.position + occupancyOffset, occupancyHalfExtents * 2f);
    }
}

/// <summary>Idempotently connects Vida death and destroys/removes enemies exactly once.</summary>
[RequireComponent(typeof(Vida))]
public sealed class ArenaEnemyRegistration : MonoBehaviour
{
    private Vida life;
    private ArenaSpawner spawner;
    private bool resolved;
    private void Awake() { life = GetComponent<Vida>(); }
    internal void Initialize(ArenaSpawner owner, ArenaManager arena) { spawner = owner; if (life == null) life = GetComponent<Vida>(); if (life != null) life.DestruirAoMorrer = false; }
    private void OnEnable() { if (life == null) life = GetComponent<Vida>(); if (life != null) life.AoMorrer += OnKilled; }
    private void OnDisable() { if (life != null) life.AoMorrer -= OnKilled; }
    private void OnKilled()
    {
        if (resolved) return; resolved = true;
        if (spawner != null) spawner.Unregister(this, true);
        foreach (ArenaEnemyAI ai in GetComponentsInChildren<ArenaEnemyAI>()) ai.enabled = false;
        foreach (ArenaEnemyWeapon weapon in GetComponentsInChildren<ArenaEnemyWeapon>()) weapon.SetCanFire(false);
        Destroy(gameObject);
    }
    private void OnDestroy() { if (!resolved && spawner != null) { resolved = true; spawner.Unregister(this, false); } }
}
