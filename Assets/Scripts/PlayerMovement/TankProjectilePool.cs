using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool FIXA de projéteis, compartilhada por qualquer um que atire: o player, os NPCs e
/// ferramentas de debug.
///
/// Ideia: projéteis são caros por causa do Instantiate/Destroy (alocação + GC). Com a pool,
/// o objeto é só ativado/desativado e reaproveitado. A capacidade é rígida — a pool nunca
/// passa de <see cref="Capacity"/>. Quando lotada, o pedido é DESCARTADO (o atirador não
/// consome o cooldown, ver TankPlayerInput) e um aviso é registrado a cada
/// <see cref="WarningInterval"/> pedidos perdidos.
///
/// Como qualquer script pede um projétil (player ou NPC):
/// <code>
/// // NPC, no estado de ataque:
/// TankProjectile p = TankProjectilePool.Spawn(muzzle.position, aimDirection);
///
/// // com velocidade/vida próprias e ignorando o próprio corpo (para não atirar em si mesmo):
/// TankProjectile p = TankProjectilePool.Spawn(muzzle.position, aimDirection,
///                                              speed: 28f, lifetime: 3f,
///                                              ignoredColliders: GetComponentsInChildren&lt;Collider&gt;(true));
/// </code>
/// Se não existir pool na cena, <see cref="Spawn(Vector3, Vector3, float, float, Collider[])"/>
/// devolve null e loga o motivo — ou o atirador cria uma com
/// <see cref="Ensure(TankProjectile, int, Transform)"/> (é o que o TankPlayerInput faz).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Projectile Pool")]
public sealed class TankProjectilePool : MonoBehaviour
{
    /// <summary>Resultado de um pedido de projétil.</summary>
    public enum RequestResult
    {
        /// <summary>Projétil obtido (novo ou reaproveitado).</summary>
        Spawned,
        /// <summary>Nenhum TankProjectilePool na cena.</summary>
        NoPool,
        /// <summary>A pool está lotada: o pedido foi descartado.</summary>
        PoolFull,
        /// <summary>A pool não tem prefab atribuído.</summary>
        NoPrefab
    }

    [Header("Pool")]
    [Tooltip("Prefab do projétil (precisa do componente TankProjectile). É o único modelo que a pool sabe criar.")]
    [SerializeField] private TankProjectile projectilePrefab;

    [SerializeField, Min(1)]
    [Tooltip("Quantidade máxima de projéteis. A pool nunca cria além disso.")]
    private int capacity = 32;

    [SerializeField, Min(0)]
    [Tooltip("Já deixa esta quantidade pronta no Awake (evita o custo da primeira criação em lote).")]
    private int prewarmCount = 8;

    [Header("Diagnóstico")]
    [Tooltip("Um aviso a cada N pedidos recusados por lotação. Zero silencia os avisos.")]
    [SerializeField, Min(0)] private int warningInterval = 20;

    // Todas as instâncias criadas (livres + em uso) e a pilha de livres.
    private readonly List<TankProjectile> instances = new List<TankProjectile>();
    private readonly List<TankProjectile> available = new List<TankProjectile>();
    // Projéteis que pediram retorno neste frame; são desativados no LateUpdate, porque o
    // pedido vem de um callback de física (OnCollisionEnter) e desligar um colisor ali gera
    // reclamação do PhysX.
    private readonly List<TankProjectile> pendingRelease = new List<TankProjectile>();

    private int totalSpawned;
    private int droppedRequests;
    private bool prefabMissingReported;

    private static TankProjectilePool instance;

    #region API pública (instância)

    /// <summary>Capacidade máxima configurada.</summary>
    public int Capacity => capacity;

    /// <summary>Quantos projéteis estão em voo agora (nem todos os criados estão livres).</summary>
    public int ActiveCount => Mathf.Max(0, instances.Count - available.Count);

    /// <summary>Quantos projéteis já foram criados (em voo + prontos na pool).</summary>
    public int CreatedCount => instances.Count;

    /// <summary>Quanto ainda dá para criar antes de a pool travar no teto.</summary>
    public int FreeSlotCount => Mathf.Max(0, capacity - instances.Count);

    /// <summary>Total de projéteis efetivamente disparados desde o Awake.</summary>
    public int TotalSpawned => totalSpawned;

    /// <summary>Pedidos recusados por lotação.</summary>
    public int DroppedRequests => droppedRequests;

    /// <summary>Prefab usado pela pool.</summary>
    public TankProjectile ProjectilePrefab => projectilePrefab;

    /// <summary>Quantos projéteis o Awake deixa prontos.</summary>
    public int PrewarmCount => prewarmCount;

    /// <summary>
    /// Pede um projétil. Velocidade/vida em zero significam "usa o valor do prefab".
    /// <paramref name="ignoredColliders"/> são os colisores de quem disparou (evita o projétil
    /// bater no próprio tanque). Devolve null se o pedido não pôde ser atendido.
    /// </summary>
    public TankProjectile RequestProjectile(Vector3 position, Vector3 direction, float speed = 0f,
        float lifetime = 0f, Collider[] ignoredColliders = null)
    {
        return RequestProjectile(position, direction, speed, lifetime, ignoredColliders, default(TankShotData), out _);
    }

    /// <summary>
     /// Igual a <see cref="RequestProjectile(Vector3, Vector3, float, float, Collider[])"/>, mas coleta
     /// sozinho os colisores do atirador (mais cômodo para NPCs).
     /// </summary>
    public TankProjectile RequestProjectileFromOwner(GameObject owner, Vector3 position, Vector3 direction,
        float speed = 0f, float lifetime = 0f)
    {
        Collider[] ownerColliders = owner != null ? owner.GetComponentsInChildren<Collider>(true) : null;
        return RequestProjectile(position, direction, speed, lifetime, ownerColliders, out _);
    }

    /// <summary>
    /// Versão com o motivo da recusa, para quem precisa decidir algo a partir dela
    /// (ex.: o player não gasta o cooldown quando a pool está lotada).
    /// </summary>
    public TankProjectile RequestProjectile(Vector3 position, Vector3 direction, float speed, float lifetime,
        Collider[] ignoredColliders, out RequestResult result)
    {
        return RequestProjectile(position, direction, speed, lifetime, ignoredColliders, default(TankShotData), out result);
    }

    public TankProjectile RequestProjectile(Vector3 position, Vector3 direction, float speed, float lifetime,
        Collider[] ignoredColliders, TankShotData shotData, out RequestResult result)
    {
        result = RequestResult.Spawned;
        TankProjectile projectile = Acquire(out result);

        if (result != RequestResult.Spawned)
        {
            ReportFailure(result);
            return null;
        }

        totalSpawned++;

        // Inicializa ainda desligado: sem este cuidado o projétil daria um FixedUpdate parado na
        // posição antiga da pool. A posição do disparo é aplicada aqui (antes, quem fazia isso era
        // o Instantiate do prefab) e a rotação vem do Initialize, que alinha o +Z com a direção.
        projectile.transform.position = position;
        projectile.Initialize(direction, speed, lifetime, ignoredColliders, shotData);
        projectile.gameObject.SetActive(true);
        return projectile;
    }

    /// <summary>
    /// Reserva <paramref name="count"/> projéteis de uma vez (usado pelo prewarm e por quem
    /// quer evitar o custo de criação no meio de uma rajada).
    /// </summary>
    public void Prewarm(int count)
    {
        for (int i = 0; i < count && instances.Count + pendingRelease.Count < capacity; i++)
        {
            if (CreateInstance(false) == null)
                break;
        }
    }

    /// <summary>
    /// Devolve todos os projéteis para a pool (em voo inclusos). Útil ao reiniciar a fase sem
    /// recarregar a cena.
    /// </summary>
    public void ReturnAll()
    {
        pendingRelease.Clear();

        for (int i = instances.Count - 1; i >= 0; i--)
        {
            TankProjectile projectile = instances[i];
            if (projectile == null)
            {
                instances.RemoveAt(i);
                continue;
            }

            if (projectile.gameObject.activeInHierarchy)
            {
                projectile.ReleaseToPool(this);
                available.Add(projectile);
            }
        }
    }

    #endregion

    #region API pública (estática, para NPCs)

    /// <summary>Pool registrada na cena (a primeira que deu Awake), ou null.</summary>
    public static TankProjectilePool Instance => instance != null ? instance : FindFirstObjectByType<TankProjectilePool>();

    /// <summary>
    /// Ponto de entrada para NPCs e qualquer outro script. Devolve null se não houver pool.
    /// </summary>
    public static TankProjectile Spawn(Vector3 position, Vector3 direction, float speed = 0f, float lifetime = 0f,
        Collider[] ignoredColliders = null)
    {
        TankProjectilePool pool = Instance;
        if (pool == null)
        {
            Debug.LogWarning("[TankProjectilePool] Nenhum TankProjectilePool na cena. " +
                             "Crie um GameObject com o componente (menu: Arena/Tank/Tank Projectile Pool) " +
                             "ou deixe o TankPlayerInput criar o seu.");
            return null;
        }

        return pool.RequestProjectile(position, direction, speed, lifetime, ignoredColliders, out _);
    }

    /// <summary>Dispara a partir de <paramref name="muzzle"/> na direção indicada (atalho para NPCs).</summary>
    public static TankProjectile SpawnAt(GameObject owner, Transform muzzle, Vector3 direction, float speed = 0f,
        float lifetime = 0f)
    {
        if (muzzle == null)
            return null;

        TankProjectilePool pool = Instance;
        if (pool == null)
            return null;

        return pool.RequestProjectileFromOwner(owner, muzzle.position, direction, speed, lifetime);
    }

    /// <summary>
    /// Garante que exista uma pool: reaproveita a da cena ou cria um GameObject próprio com o
    /// prefab e a capacidade informados. Chamado pelo TankPlayerInput quando ele não tem pool.
    /// </summary>
    public static TankProjectilePool Ensure(TankProjectile prefab, int capacity, Transform parent = null)
    {
        TankProjectilePool pool = Instance;

        if (pool == null)
        {
            GameObject go = new GameObject("PoolDeProjetis");

            // Desligado durante a configuração: AddComponent dispararia o Awake (e o prewarm)
            // com o prefab ainda vazio. Ativando depois, o prewarm já encontra tudo pronto.
            go.SetActive(false);
            if (parent != null)
                go.transform.SetParent(parent, false);

            pool = go.AddComponent<TankProjectilePool>();
            ConfigureAndActivate(pool, prefab, capacity);
        }
        else if (pool.projectilePrefab == null && prefab != null)
        {
            // Pool já existente (montada na cena) sem prefab: aproveita o do atirador, mas não
            // mexe na Capacity — quem configurou a pool na cena é o usuário.
            pool.projectilePrefab = prefab;
            pool.Prewarm(pool.PrewarmCount);
        }

        return pool;
    }

    #endregion

    private static void ConfigureAndActivate(TankProjectilePool pool, TankProjectile prefab, int capacity)
    {
        if (prefab != null)
            pool.projectilePrefab = prefab;

        // Pool recém-criada: quem define a capacidade é o atirador que pediu Ensure, e não o
        // valor default do componente (32) — do contrário o Inspector ainda não foi visto.
        pool.capacity = Mathf.Max(1, capacity);
        pool.prewarmCount = Mathf.Clamp(pool.prewarmCount, 0, pool.capacity);
        pool.gameObject.SetActive(true);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[{nameof(TankProjectilePool)}] Existe mais de uma pool na cena " +
                             $"'{name}' e '{instance.name}'. Os pedidos estáticos vão para a primeira; " +
                             "deixe só uma ou referencie a pool diretamente em cada atirador.", this);
        }
        else
        {
            instance = this;
            capacity = Mathf.Max(1, capacity);
            Prewarm(Mathf.Min(prewarmCount, capacity));
        }
    }

    private void OnEnable()
    {
        if (instance == null)
            instance = this;
    }

    private void OnDisable()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// Chamado pelo <see cref="TankProjectile.RequestDespawn"/>. Guarda o projétil para o
    /// recolhimento no LateUpdate (fora, portanto, do callback de física que o originou).
    /// </summary>
    public void EnqueueRelease(TankProjectile projectile)
    {
        if (projectile == null || pendingRelease.Contains(projectile))
            return;

        pendingRelease.Add(projectile);
    }

    private void LateUpdate()
    {
        if (pendingRelease.Count == 0)
            return;

        for (int i = 0; i < pendingRelease.Count; i++)
        {
            TankProjectile projectile = pendingRelease[i];
            if (projectile == null || !projectile.gameObject.activeInHierarchy)
                continue;

            if (projectile.ReleaseToPool(this))
            {
                available.Add(projectile);
            }
            else
            {
                // A pool não era a dona deste projétil: ele foi destruído e sai da lista.
                instances.Remove(projectile);
            }
        }

        pendingRelease.Clear();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        for (int i = instances.Count - 1; i >= 0; i--)
        {
            TankProjectile projectile = instances[i];
            if (projectile != null)
                Destroy(projectile.gameObject);
        }

        instances.Clear();
        available.Clear();
        pendingRelease.Clear();
    }

    private TankProjectile Acquire(out RequestResult result)
    {
        result = RequestResult.Spawned;

        if (projectilePrefab == null)
        {
            result = RequestResult.NoPrefab;
            return null;
        }

        // Alguém pode ter destruído um projétil por fora (script de dano, debug).
        // Sem esta limpeza, a pool contaria instâncias fantasmas e perderia capacidade.
        PruneDestroyed();

        // Reaproveita um projétil livre.
        while (available.Count > 0)
        {
            int last = available.Count - 1;
            TankProjectile projectile = available[last];
            available.RemoveAt(last);

            if (projectile == null)
                continue;

            projectile.ClaimFromPool(this);
            return projectile;
        }

        // Ainda há espaço para criar novas instâncias: cria agora (sem passar do teto).
        TankProjectile created = CreateInstance(true);
        if (created != null)
            return created;

        result = RequestResult.PoolFull;
        return null;
    }

    /// <summary>
    /// Cria uma instância do prefab. Com <paramref name="claimForUse"/> ela já vem marcada como
    /// em uso; sem ela entra na pilha de livres (caso do prewarm).
    /// </summary>
    private TankProjectile CreateInstance(bool claimForUse)
    {
        if (projectilePrefab == null || instances.Count >= capacity)
            return null;

        GameObject go = Instantiate(projectilePrefab.gameObject, transform.position, transform.rotation, transform);
        go.name = $"{projectilePrefab.gameObject.name} [Pooled {instances.Count + 1:00}]";

        TankProjectile projectile = go.GetComponent<TankProjectile>();
        if (projectile == null)
        {
            Destroy(go);
            ReportFailure(RequestResult.NoPrefab);
            return null;
        }

        instances.Add(projectile);

        if (claimForUse)
            projectile.ClaimFromPool(this);
        else
            available.Add(projectile);

        go.SetActive(false);
        return projectile;
    }

    private void PruneDestroyed()
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i] != null)
                continue;

            instances.RemoveAt(i);
        }

        for (int i = available.Count - 1; i >= 0; i--)
        {
            if (available[i] != null)
                continue;

            available.RemoveAt(i);
        }

        for (int i = pendingRelease.Count - 1; i >= 0; i--)
        {
            if (pendingRelease[i] != null)
                continue;

            pendingRelease.RemoveAt(i);
        }
    }

    private void ReportFailure(RequestResult result)
    {
        switch (result)
        {
            case RequestResult.NoPool:
                Debug.LogWarning($"[{nameof(TankProjectilePool)}] Pedido feito sem pool na cena.");
                break;

            case RequestResult.NoPrefab:
                if (prefabMissingReported)
                    return;
                prefabMissingReported = true;
                Debug.LogError($"[{nameof(TankProjectilePool)}] '{name}' sem Projectile Prefab definido. " +
                               "Arraste o prefab do projétil no componente.", this);
                break;

            case RequestResult.PoolFull:
                droppedRequests++;
                if (warningInterval <= 0)
                    return;
                if (droppedRequests % warningInterval == 0)
                {
                    Debug.LogWarning($"[{nameof(TankProjectilePool)}] Pool de '{name}' lotada ({capacity} projéteis). " +
                                     $"{droppedRequests} pedido(s) descartado(s). Aumente a Capacity ou o dano/vida " +
                                     "dos projéteis se isso virar gargalo.");
                }
                break;
        }
    }

    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        prewarmCount = Mathf.Clamp(prewarmCount, 0, capacity);

        if (instances.Count > capacity)
            Debug.LogWarning($"[{nameof(TankProjectilePool)}] Capacity ({capacity}) menor que o já criado " +
                             $"({instances.Count}). A pool continua funcionando, mas não vai reaproveitar " +
                             "até o número cair.", this);
    }

    /// <summary>Snapshot para GUI/debug: (em voo, criados, capacidade, total disparado, recusados).</summary>
    public void GetStats(out int active, out int created, out int capacityValue, out int spawned, out int dropped)
    {
        active = instances.Count - available.Count;
        created = instances.Count;
        capacityValue = capacity;
        spawned = totalSpawned;
        dropped = droppedRequests;
    }
}
