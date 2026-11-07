using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lê o input do jogador pelo Input System e repassa para o <see cref="TankMovement"/> e o disparo do tanque.
///
/// Espera, no asset de Input Actions, um mapa (padrão "Tank") com:
///   - Uma ação "Move" (Value/Vector2):
///       * Y = frente (+) / trás (-)            → W/S, setas cima/baixo, analógico esquerdo
///       * X = girar direita (+) / esquerda (-) → A/D, setas esquerda/direita, analógico esquerdo
///   - Uma ação "Fire" (Button):
///       * Botão esquerdo do mouse, espaço, gatilho direito (RT/R2), botão A/Cross, RB/R1, toque na tela, etc.
/// </summary>
[RequireComponent(typeof(TankMovement))]
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Player Input")]
public class TankPlayerInput : MonoBehaviour
{
    [Header("Input System")]
    [Tooltip("Asset de Input Actions (ex.: TankInputActions) que contém o mapa e as ações de movimento e tiro.")]
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Nome do Action Map dentro do asset.")]
    [SerializeField] private string actionMapName = "Tank";

    [Tooltip("Nome da ação Vector2 de movimento dentro do Action Map.")]
    [SerializeField] private string moveActionName = "Move";

    [Tooltip("Nome da ação Button de disparo dentro do Action Map.")]
    [SerializeField] private string fireActionName = "Fire";

    [Header("Referências")]
    [Tooltip("Componente de movimentação que receberá o input. Se vazio, usa o do próprio GameObject.")]
    [SerializeField] private TankMovement movement;

    [Tooltip("ParticleSystem de tiro (ex.: FX_Tiro). Se vazio, busca automaticamente nos filhos (ex.: no Trabuco).")]
    [SerializeField] private ParticleSystem shootVFX;

    [Tooltip("Luz de clarão do tiro (ex.: FX_TiroBrilho). Se vazio, busca nos filhos do FX_Tiro.")]
    [SerializeField] private Light flashLight;

    [Tooltip("Transform cuja frente (+Z) representa a direção do canhão (ex.: Trabuco).")]
    [SerializeField] private Transform barrelTransform;

    [Tooltip("Ponto de origem onde o projétil nasce (ex.: FX_Tiro na ponta do Trabuco).")]
    [SerializeField] private Transform firePoint;

    [Header("Configurações do Disparo")]
    [Tooltip("Cadência / recarga: intervalo mínimo entre disparos em segundos. É o tempo que o " +
             "retículo do canhão leva para completar a animação de recarga.")]
    [SerializeField, Min(0f)] private float fireRate = 0.2f;

    [Tooltip("Duração do clarão da luz (FX_TiroBrilho) a cada disparo.")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.05f;

    [Header("Projéteis (pool compartilhada com os NPCs)")]
    [Tooltip("Pool que fornece os projéteis. Se vazio, usa a pool da cena; se não houver nenhuma, " +
             "o próprio script cria um GameObject PoolDeProjetis com o prefab e a capacidade abaixo.")]
    [SerializeField] private TankProjectilePool projectilePool;

    [Tooltip("Prefab do projétil pedido à pool.")]
    [SerializeField] private TankProjectile projectilePrefab;

    [Tooltip("Capacidade da pool criada por este script quando não existe pool na cena. " +
             "Se já houver pool, quem manda é a Capacity dela.")]
    [SerializeField, Min(1)] private int poolCapacity = 32;

    [Tooltip("Velocidade de deslocamento do projétil disparado.")]
    [SerializeField, Min(0.1f)] private float projectileSpeed = 50f;

    [Tooltip("Tempo máximo de vida (em segundos) do projétil disparado.")]
    [SerializeField, Min(0.1f)] private float projectileLifetime = 5f;

    [SerializeField, Min(0f)] private float projectileDamage = 50f;

    private InputAction moveAction;
    private InputAction fireAction;
    private float nextFireTime;
    private Coroutine flashCoroutine;
    private Collider[] tankColliders;
    private bool poolReported;
    private int shotsFired;

    /// <summary>
    /// Quantos disparos realmente saíram. O HUD observa esse contador para saber o instante exato
    /// de começar a animação de recarga (mais confiável que comparar timestamps).
    /// </summary>
    public int ShotsFired => shotsFired;

    /// <summary>Intervalo de recarga entre disparos, em segundos.</summary>
    public float FireRate => fireRate;

    /// <summary>Instante (em segundos) a partir do qual o próximo disparo é liberado.</summary>
    public float NextFireTime => nextFireTime;

    /// <summary>True quando já pode disparar de novo.</summary>
    public bool IsReady => Time.time >= nextFireTime;

    /// <summary>Quanto falta para liberar o próximo disparo, em segundos.</summary>
    public float CooldownRemaining => Mathf.Max(0f, nextFireTime - Time.time);

    /// <summary>Quanto do cooldown já passou, em segundos (0 quando está pronto).</summary>
    public float CooldownElapsed => Mathf.Max(0f, fireRate - CooldownRemaining);

    /// <summary>
    /// Progresso da recarga de 0 (recém-disparado) a 1 (pronto). É o valor que o HUD usa para
    /// preencher o retículo do canhão; com fireRate zero, está sempre pronto.
    /// </summary>
    public float CooldownProgress => fireRate <= 0f || nextFireTime <= 0f
        ? 1f
        : Mathf.Clamp01(1f - CooldownRemaining / fireRate);

    /// <summary>Pool em uso por este atirador.</summary>
    public TankProjectilePool ProjectilePool => projectilePool;

    /// <summary>
    /// Ajusta dano e cadência em runtime (usado pelos PlayerStats). Zero é ignorado.
    /// </summary>
    public void Configure(float damage, float rate)
    {
        if (damage > 0f) projectileDamage = damage;
        if (rate > 0f) fireRate = rate;
    }

    private void Reset()
    {
        movement = GetComponent<TankMovement>();
        projectilePool = GetComponentInParent<TankProjectilePool>();
        FindShootReferences();
    }

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<TankMovement>();

        FindShootReferences();
        tankColliders = GetComponentsInChildren<Collider>(true);

        // Pool: usa a atribuída, a da cena, ou cria uma com o prefab e a capacidade daqui.
        // NPCs chamam o mesmo ponto de entrada (TankProjectilePool.Spawn), então todos os
        // projéteis da partida saem de um único conjunto de objetos reutilizados.
        projectilePool = TankProjectilePool.Ensure(projectilePrefab, poolCapacity);
        if (projectilePool != null && projectilePrefab == null)
            projectilePrefab = projectilePool.ProjectilePrefab;

        if (shootVFX != null)
        {
            var main = shootVFX.main;
            main.loop = false;
            main.playOnAwake = false;
        }

        if (flashLight != null)
            flashLight.enabled = false;

        if (inputActions == null)
        {
            Debug.LogError($"[{nameof(TankPlayerInput)}] Nenhum InputActionAsset atribuído em '{name}'. " +
                           "Arraste o asset TankInputActions para o campo 'Input Actions'.", this);
            enabled = false;
            return;
        }

        // throwIfNotFound = true: falha cedo e com mensagem clara se o mapa/ação foram renomeados.
        InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
        moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
        fireAction = map.FindAction(fireActionName, throwIfNotFound: false);

        if (fireAction == null)
        {
            Debug.LogWarning($"[{nameof(TankPlayerInput)}] Ação '{fireActionName}' não encontrada no mapa '{actionMapName}'. " +
                             "Verifique o asset de Input Actions.", this);
        }
    }

    private void OnEnable()
    {
        moveAction?.Enable();

        if (fireAction != null)
        {
            fireAction.performed += OnFirePerformed;
            fireAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (fireAction != null)
        {
            fireAction.performed -= OnFirePerformed;
            fireAction.Disable();
        }

        moveAction?.Disable();

        // Evita que o tanque continue andando com o último input recebido.
        if (movement != null)
            movement.SetInput(0f, 0f);
    }

    private void Update()
    {
        if (moveAction != null && movement != null)
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            movement.SetInput(throttle: move.y, steer: move.x);
        }
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        TryFire();
    }

    /// <summary>
    /// Dispara o projétil sincronizado com a direção do canhão, o efeito de partículas FX_Tiro
    /// e ativa o clarão da luz temporariamente.
    /// Pode ser chamado externamente por outros scripts ou botões de UI.
    ///
    /// Se o projétil não puder ser obtido (pool inexistente, sem prefab ou lotada), o disparo é
    /// cancelado e o cooldown NÃO é consumido — o jogador pode tentar de novo no mesmo frame.
    /// </summary>
    public bool TryFire()
    {
        if (Time.time < nextFireTime)
            return false;

        if (!TryRequestProjectile(out TankProjectilePool.RequestResult result))
        {
            ReportPoolProblem(result);
            return false;
        }

        nextFireTime = Time.time + fireRate;
        shotsFired++;

        // Dispara o Particle System do tiro
        if (shootVFX != null)
        {
            shootVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            shootVFX.Play(true);
        }

        // Clarão rápido de luz
        if (flashLight != null && gameObject.activeInHierarchy)
        {
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine());
        }

        return true;
    }

    /// <summary>
    /// Pede um projétil à pool na boca do cano, na direção atual do canhão.
    /// É o mesmo caminho que um NPC usa — a diferença é só quem fornece origem e direção.
    /// </summary>
    private bool TryRequestProjectile(out TankProjectilePool.RequestResult result)
    {
        Vector3 direction = barrelTransform != null && barrelTransform.forward.sqrMagnitude > 0.0001f
            ? barrelTransform.forward.normalized
            : (firePoint != null && firePoint.forward.sqrMagnitude > 0.0001f
                ? firePoint.forward.normalized
                : transform.forward);

        Vector3 spawnPosition = firePoint != null
            ? firePoint.position
            : (barrelTransform != null ? barrelTransform.position + direction * 1.5f : transform.position + direction * 2f);

        if (tankColliders == null || tankColliders.Length == 0)
            tankColliders = GetComponentsInChildren<Collider>(true);

        if (projectilePool == null)
            projectilePool = TankProjectilePool.Ensure(projectilePrefab, poolCapacity);

        if (projectilePool == null)
        {
            result = TankProjectilePool.RequestResult.NoPool;
            return false;
        }

        projectilePool.RequestProjectile(spawnPosition, direction, projectileSpeed, projectileLifetime,
            tankColliders, new TankShotData(gameObject, TankShotFaction.Player, projectileDamage), out result);

        return result == TankProjectilePool.RequestResult.Spawned;
    }

    private void ReportPoolProblem(TankProjectilePool.RequestResult result)
    {
        if (poolReported || result == TankProjectilePool.RequestResult.PoolFull)
            return;

        // Um aviso por sessão para cada problema de configuração; a recusa em si é tratada na pool.
        if (result == TankProjectilePool.RequestResult.NoPrefab)
        {
            poolReported = true;
            Debug.LogError($"[{nameof(TankPlayerInput)}] Sem prefab de projétil. Atribua Projectile Prefab em " +
                           $"'{name}' (ou no componente TankProjectilePool da cena).", this);
        }
    }

    private IEnumerator FlashRoutine()
    {
        flashLight.enabled = true;
        yield return new WaitForSeconds(flashDuration);
        flashLight.enabled = false;
        flashCoroutine = null;
    }

    private void FindShootReferences()
    {
        if (shootVFX == null)
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                if (ps.name.Equals("FX_Tiro", System.StringComparison.OrdinalIgnoreCase))
                {
                    shootVFX = ps;
                    break;
                }
            }

            if (shootVFX == null && systems.Length > 0)
                shootVFX = systems[0];
        }

        if (flashLight == null && shootVFX != null)
        {
            flashLight = shootVFX.GetComponentInChildren<Light>(true);
        }

        if (barrelTransform == null)
        {
            TankTurretAim turretAim = GetComponent<TankTurretAim>();
            if (turretAim != null && turretAim.AimReference != null)
            {
                barrelTransform = turretAim.AimReference;
            }
            else if (shootVFX != null && shootVFX.transform.parent != null)
            {
                barrelTransform = shootVFX.transform.parent;
            }
            else
            {
                Transform[] children = GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child.name.Equals("Trabuco", System.StringComparison.OrdinalIgnoreCase))
                    {
                        barrelTransform = child;
                        break;
                    }
                }
            }
        }

        if (firePoint == null)
        {
            if (shootVFX != null)
                firePoint = shootVFX.transform;
            else if (barrelTransform != null)
                firePoint = barrelTransform;
        }
    }
}
