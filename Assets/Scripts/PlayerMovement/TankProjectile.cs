using UnityEngine;

public enum TankShotFaction { Neutral, Player, Enemy }

/// <summary>Explicit per-shot provenance and damage; never stored globally.</summary>
public struct TankShotData
{
    public readonly GameObject Shooter;
    public readonly TankShotFaction Faction;
    public readonly float Damage;
    public TankShotData(GameObject shooter, TankShotFaction faction, float damage) { Shooter = shooter; Faction = faction; Damage = Mathf.Max(0f, damage); }
}

/// <summary>
/// Projétil disparado pelo canhão do tanque.
/// Desloca-se em linha reta na direção do canhão no momento do disparo.
///
/// Ele não se destrói mais: quem o pediu (a <see cref="TankProjectilePool"/>) o recolhe no fim
/// da vida útil ou no impacto, e o objeto volta para a pool. Projéteis criados fora da pool
/// (Instantiate direto, prefab solto na cena) mantêm o comportamento antigo de Destroy, para o
/// script continuar útil sem pool.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Projectile")]
public sealed class TankProjectile : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Velocidade de deslocamento do projétil em unidades por segundo.")]
    [SerializeField, Min(0.1f)] private float speed = 50f;

    [Tooltip("Tempo máximo de vida (em segundos) antes de o projétil ser recolhido.")]
    [SerializeField, Min(0.1f)] private float maxLifetime = 5f;

    private Rigidbody body;
    private Collider projectileCollider;
    private Vector3 moveDirection;
    private bool initialized;
    private GameObject shooter;
    private TankShotFaction faction;
    private float damage;
    private float prefabSpeed;
    private float prefabLifetime;
    private bool defaultsCaptured;
    private bool impactResolved;

    // Controle de pool: tempo restante, pedido de recolhimento pendente, dona e colisores que
    // foram ignorados neste uso (precisam voltar a colidir antes do próximo atirador).
    private float despawnTime;
    private bool despawnRequested;
    private TankProjectilePool sourcePool;
    private Collider[] ignoredColliders;

    /// <summary>Direção do disparo no espaço do mundo.</summary>
    public Vector3 MoveDirection => moveDirection;

    /// <summary>Pool que forneceu este projétil (null quando ele não veio de uma pool).</summary>
    public TankProjectilePool SourcePool => sourcePool;

    /// <summary>Velocidade atual em unidades por segundo.</summary>
    public float Speed => speed;
    public GameObject Shooter => shooter;
    public TankShotFaction Faction => faction;
    public float Damage => damage;

    /// <summary>Segundos que faltam para o recolhimento automático.</summary>
    public float RemainingLifetime => Mathf.Max(0f, despawnTime - Time.time);

    private void Awake()
    {
        EnsureComponents();
        ConfigureRigidbody();
        CaptureDefaults();
    }

    private void CaptureDefaults()
    {
        if (defaultsCaptured) return;
        prefabSpeed = speed;
        prefabLifetime = maxLifetime;
        defaultsCaptured = true;
    }

    private void Start()
    {
        // Permite usar o prefab sem pool nem script de disparo: segue a frente do próprio objeto.
        if (!initialized)
            Initialize(transform.forward, 0f, 0f, null);
    }

    /// <summary>
    /// Inicializa o projétil com a direção do canhão, velocidade, vida e colisores a ignorar.
    /// Velocidade/vida em zero significam "mantém o valor do prefab".
    /// </summary>
    public void Initialize(Vector3 barrelDirection, float projectileSpeed, float lifetime, Collider[] ignoredColliders)
    {
        Initialize(barrelDirection, projectileSpeed, lifetime, ignoredColliders, default(TankShotData));
    }

    public void Initialize(Vector3 barrelDirection, float projectileSpeed, float lifetime, Collider[] ignoredColliders, TankShotData shotData)
    {
        EnsureComponents();
        ConfigureRigidbody();
        CaptureDefaults();

        moveDirection = barrelDirection.sqrMagnitude > 0.0001f
            ? barrelDirection.normalized
            : transform.forward;

        // Every reuse starts from prefab defaults; overrides from an earlier shot never leak.
        speed = prefabSpeed > 0f ? prefabSpeed : speed;
        maxLifetime = prefabLifetime > 0f ? prefabLifetime : maxLifetime;
        if (projectileSpeed > 0f) speed = projectileSpeed;
        if (lifetime > 0f) maxLifetime = lifetime;

        transform.forward = moveDirection;

        shooter = shotData.Shooter; faction = shotData.Faction; damage = shotData.Damage; impactResolved = false;
        ApplyIgnoredColliders(ignoredColliders);

        despawnRequested = false;
        despawnTime = Time.time + maxLifetime;
        initialized = true;

        if (body != null)
        {
            body.linearVelocity = moveDirection * speed;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (despawnRequested)
            return;

        if (Time.time >= despawnTime)
            RequestDespawn();
    }

    private void FixedUpdate()
    {
        if (body == null || despawnRequested)
            return;

        // Mantém a velocidade constante e perfeitamente alinhada com a direção do disparo.
        body.linearVelocity = moveDirection * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (impactResolved || collision == null || collision.collider == null) return;
        Transform hitRoot = collision.collider.transform.root;
        Transform shooterRoot = shooter != null ? shooter.transform.root : null;
        // Defensive owner check complements IgnoreCollision and protects unusual child-collider layouts.
        if (shooterRoot != null && hitRoot == shooterRoot) return;
        impactResolved = true;
        if (ArenaManager.IsPlaying && damage > 0f && faction != TankShotFaction.Neutral)
        {
            Vida life = collision.collider.GetComponentInParent<Vida>();
            if (life != null && !life.Morto)
            {
                bool isPlayer = hitRoot.CompareTag("Player") || (ArenaManager.Instance != null && life == ArenaManager.Instance.PlayerLife);
                bool isEnemy = collision.collider.GetComponentInParent<ArenaEnemyRegistration>() != null || collision.collider.GetComponentInParent<ArenaEnemyAI>() != null;
                if ((faction == TankShotFaction.Player && isEnemy) || (faction == TankShotFaction.Enemy && isPlayer))
                    life.ReceberDano(damage);
            }
        }
        RequestDespawn();
    }

    /// <summary>
    /// Fora da pool, equivale ao antigo Destroy(gameObject): recolhe o projétil agora.
    /// Dentro da pool, apenas marca e zera a velocidade; o desligamento do GameObject acontece
    /// no LateUpdate da pool, porque desativar um colisor durante um callback de física é o
    /// caminho clássico para warnings e para o restante da simulação ver um collider sumido.
    /// </summary>
    public void RequestDespawn()
    {
        if (despawnRequested)
            return;

        despawnRequested = true;

        if (body != null)
            body.linearVelocity = Vector3.zero;

        if (sourcePool != null)
            sourcePool.EnqueueRelease(this);
        else
            Destroy(gameObject);
    }

    /// <summary>Chamado pela pool ao entregar o projétil. Use <see cref="RequestDespawn"/> para devolver.</summary>
    public void ClaimFromPool(TankProjectilePool pool)
    {
        sourcePool = pool;
        shooter = null; faction = TankShotFaction.Neutral; damage = 0f; impactResolved = false;
        initialized = false;
        despawnRequested = false;
    }

    /// <summary>
    /// Chamado pela pool no LateUpdate. Devolve false se este projétil não pertence à pool que
    /// pediu o recolhimento (nesse caso ele é destruído, para não vazar um objeto órfão).
    /// </summary>
    public bool ReleaseToPool(TankProjectilePool pool)
    {
        if (pool == null || sourcePool != pool)
        {
            if (this != null)
                Destroy(gameObject);
            return false;
        }

        ClearIgnoredColliders();

        EnsureComponents();

        if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }

        moveDirection = Vector3.zero;
        speed = prefabSpeed > 0f ? prefabSpeed : speed;
        maxLifetime = prefabLifetime > 0f ? prefabLifetime : maxLifetime;
        despawnTime = 0f;
        shooter = null; faction = TankShotFaction.Neutral; damage = 0f; impactResolved = false;
        initialized = false;
        despawnRequested = false;
        sourcePool = null;

        gameObject.SetActive(false);
        return true;
    }

    private void EnsureComponents()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        if (projectileCollider == null)
            projectileCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// Ignora os colisores do atirador para o projétil não bater na boca do próprio cano.
    /// Os pares anteriores são liberados antes: como <see cref="Physics.IgnoreCollision"/> é
    /// permanente por par, sem isso o projétil reutilizado atravessaria o tanque do atirador
    /// anterior.
    /// </summary>
    private void ApplyIgnoredColliders(Collider[] newIgnoredColliders)
    {
        ClearIgnoredColliders();

        if (newIgnoredColliders == null || newIgnoredColliders.Length == 0)
            return;

        ignoredColliders = newIgnoredColliders;

        // Sem colisor próprio não há par para ignorar (e Physics.IgnoreCollision reclamaria de null).
        if (projectileCollider == null)
            return;

        for (int i = 0; i < newIgnoredColliders.Length; i++)
        {
            Collider col = newIgnoredColliders[i];
            if (col != null && col != projectileCollider)
                Physics.IgnoreCollision(projectileCollider, col, true);
        }
    }

    private void ClearIgnoredColliders()
    {
        if (ignoredColliders == null)
            return;

        if (projectileCollider != null)
        {
            for (int i = 0; i < ignoredColliders.Length; i++)
            {
                Collider col = ignoredColliders[i];
                if (col != null && col != projectileCollider)
                    Physics.IgnoreCollision(projectileCollider, col, false);
            }
        }

        ignoredColliders = null;
    }

    private void ConfigureRigidbody()
    {
        if (body == null)
            return;

        body.useGravity = false;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        maxLifetime = Mathf.Max(0.1f, maxLifetime);
    }
}
