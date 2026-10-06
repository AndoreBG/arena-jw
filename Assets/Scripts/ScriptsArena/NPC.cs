using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    [Header("Chase Settings")]
    [SerializeField] private float chaseRange = 20f;
    [SerializeField] private float stopDistance = 5f;

    [Header("Shoot Settings")]
    [SerializeField] private float shootRange = 10f;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float fireRate = 1.5f;

    private NavMeshAgent agent;
    private Transform player;
    private float nextFireTime = 0f;

    private enum State { Idle, Chasing, Shooting }
    private State currentState = State.Idle;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        // Tentativa 1: pela tag "Player"
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            return;
        }

        // Tentativa 2: pelo componente Player
        Player playerComponent = FindObjectOfType<Player>();
        if (playerComponent != null)
        {
            player = playerComponent.transform;
            return;
        }

        Debug.LogWarning("NPC: Player nao encontrado! Certifique-se que o Player tem a tag 'Player' ou possui o componente Player.");
    }

    private void Update()
    {
        // Tenta encontrar o player caso ainda nao tenha referencia
        if (player == null)
        {
            FindPlayer();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        UpdateState(distanceToPlayer);

        switch (currentState)
        {
            case State.Idle:
                agent.isStopped = true;
                break;

            case State.Chasing:
                Chase();
                break;

            case State.Shooting:
                FacePlayer();
                TryShoot();
                break;
        }
    }

    private void UpdateState(float distance)
    {
        if (distance <= shootRange)
            currentState = State.Shooting;
        else if (distance <= chaseRange)
            currentState = State.Chasing;
        else
            currentState = State.Idle;
    }

    private void Chase()
    {
        agent.isStopped = false;
        agent.stoppingDistance = stopDistance;
        agent.SetDestination(player.position);
    }

    private void FacePlayer()
    {
        agent.isStopped = true;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 8f);
        }
    }

    private void TryShoot()
    {
        if (Time.time < nextFireTime) return;
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("NPC: Assign 'bulletPrefab' e 'firePoint' no Inspector!");
            return;
        }

        Shoot();
        nextFireTime = Time.time + fireRate;
    }

    private void Shoot()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 direction = (player.position - firePoint.position).normalized;
            rb.linearVelocity = direction * bulletSpeed;
        }

        Destroy(bullet, 4f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
