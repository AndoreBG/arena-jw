using UnityEngine;

/// <summary>
/// Arma da nave alienígena. A nave não tem cano: o tiro nasce no <c>transform.position</c>
/// e segue a frente (<c>transform.forward</c>) da própria nave.
///
/// Duas responsabilidades, nesta ordem: girar a nave no eixo Y para encarar o jogador e,
/// quando a mira estiver alinhada, disparar. É este componente — e não o NavMeshAgent — quem
/// define a rotação do eixo Y (<c>NavMeshAgent.updateRotation = false</c>).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Enemy/Alien Ship Weapon")]
public sealed class AlienShipWeapon : MonoBehaviour
{
    [Header("Alvo")]
    [SerializeField] private Transform target;

    [Header("Rotação (yaw)")]
    [Tooltip("Velocidade de rotação no eixo Y, em graus por segundo. Zero gira instantaneamente.")]
    [SerializeField, Min(0f)] private float turnSpeed = 180f;

    [Tooltip("Ângulo máximo (graus) entre a frente da nave e o alvo para liberar o disparo.")]
    [SerializeField, Min(0f)] private float aimTolerance = 12f;

    [Header("Disparo")]
    [SerializeField, Min(0f)] private float fireRange = 22f;
    [SerializeField, Min(0.05f)] private float fireInterval = 1.6f;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 34f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
    [SerializeField, Min(0f)] private float damage = 10f;

    [Tooltip("Deslocamento vertical do ponto de mira (a altura do jogador).")]
    [SerializeField] private float targetHeight = 1.2f;

    [Tooltip("Espalhamento do tiro, em metros, aplicado no ponto de nascimento do projétil.")]
    [SerializeField, Min(0f)] private float spread = 0.2f;

    [Header("Linha de visão")]
    [Tooltip("Só atira quando há linha de visão livre até o jogador (não atravessa paredes).")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Pool")]
    [SerializeField] private TankProjectilePool projectilePool;

    private Collider[] ownerColliders;
    private float nextFireTime;
    private float intervalMultiplier = 1f;
    private bool canFire;

    public bool CanFire => canFire;
    public Transform Target => target;

    private void Awake()
    {
        ownerColliders = GetComponentsInChildren<Collider>(true);
        if (projectilePool == null) projectilePool = TankProjectilePool.Instance;
    }

    public void SetTarget(Transform value) => target = value;
    public void SetCanFire(bool value) => canFire = value;

    /// <summary>Dificuldade: &gt;1 deixa o disparo mais lento, 1 é o ritmo base.</summary>
    public void SetFireIntervalMultiplier(float value) => intervalMultiplier = Mathf.Max(0.1f, value);

    /// <summary>Dificuldade: tolerância de mira em graus (maior = erra mais).</summary>
    public void SetAimTolerance(float degrees) => aimTolerance = Mathf.Max(0f, degrees);

    private void Update()
    {
        if (target == null)
            return;

        Vector3 aimPoint = target.position + Vector3.up * targetHeight;
        Vector3 flat = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);

        if (flat.sqrMagnitude > 0.0004f)
        {
            Quaternion desired = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = turnSpeed <= 0f
                ? desired
                : Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
        }

        if (!canFire || !ArenaManager.IsPlaying)
            return;

        Vector3 delta = aimPoint - transform.position;
        if (delta.magnitude > fireRange)
            return;

        Vector3 flatDirection = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (flatDirection.sqrMagnitude > 0.0004f &&
            Vector3.Angle(transform.forward, flatDirection) > aimTolerance)
            return;

        if (Time.time < nextFireTime)
            return;

        if (requireLineOfSight && !HasLineOfSight(aimPoint))
            return;

        Fire(delta.normalized);
    }

    /// <summary>Verdadeiro quando o primeiro obstáculo no caminho é o próprio alvo.</summary>
    private bool HasLineOfSight(Vector3 aimPoint)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (!Physics.Linecast(origin, aimPoint, out RaycastHit hit, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return true;

        Transform hitTransform = hit.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform) ||
               (target != null && (hitTransform == target || hitTransform.IsChildOf(target)));
    }

    private void Fire(Vector3 direction)
    {
        if (projectilePool == null) projectilePool = TankProjectilePool.Instance;
        if (projectilePool == null)
            return;

        Vector3 origin = transform.position;
        if (spread > 0f)
        {
            direction = (direction + Random.insideUnitSphere * (spread * 0.05f)).normalized;
            origin += Random.insideUnitSphere * spread;
        }

        TankShotData shot = new TankShotData(gameObject, TankShotFaction.Enemy, damage);
        TankProjectile projectile = projectilePool.RequestProjectile(
            origin, direction, projectileSpeed, projectileLifetime, ownerColliders, shot, out _);

        // Só consome o intervalo quando o tiro realmente saiu (mesma regra do player).
        if (projectile != null)
            nextFireTime = Time.time + Mathf.Max(0.05f, fireInterval) * intervalMultiplier;
    }
}
