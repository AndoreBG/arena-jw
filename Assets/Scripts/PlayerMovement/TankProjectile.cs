using UnityEngine;

/// <summary>
/// Projétil disparado pelo canhão do tanque.
/// Desloca-se em linha reta de forma síncrona com a direção do canhão no momento do disparo
/// e se destrói ao colidir com qualquer superfície.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Projectile")]
public sealed class TankProjectile : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Velocidade de deslocamento do projétil em unidades por segundo.")]
    [SerializeField, Min(0.1f)] private float speed = 50f;

    [Tooltip("Tempo máximo de vida (em segundos) antes de o projétil ser destruído automaticamente.")]
    [SerializeField, Min(0.1f)] private float maxLifetime = 5f;

    private Rigidbody body;
    private Collider projectileCollider;
    private Vector3 moveDirection;
    private bool initialized;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();

        ConfigureRigidbody();
    }

    private void Start()
    {
        if (!initialized)
        {
            Initialize(transform.forward, speed, maxLifetime, null);
        }
    }

    /// <summary>
    /// Inicializa o projétil com a direção exata do canhão do tanque, velocidade e colliders a ignorar.
    /// </summary>
    public void Initialize(Vector3 barrelDirection, float projectileSpeed, float lifetime, Collider[] ignoredColliders)
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        if (projectileCollider == null)
            projectileCollider = GetComponent<Collider>();

        ConfigureRigidbody();

        moveDirection = barrelDirection.sqrMagnitude > 0.0001f
            ? barrelDirection.normalized
            : transform.forward;

        if (projectileSpeed > 0f)
            speed = projectileSpeed;

        if (lifetime > 0f)
            maxLifetime = lifetime;

        transform.forward = moveDirection;

        if (ignoredColliders != null && projectileCollider != null)
        {
            for (int i = 0; i < ignoredColliders.Length; i++)
            {
                Collider col = ignoredColliders[i];
                if (col != null && col != projectileCollider)
                {
                    Physics.IgnoreCollision(projectileCollider, col, true);
                }
            }
        }

        body.linearVelocity = moveDirection * speed;
        initialized = true;

        Destroy(gameObject, maxLifetime);
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        // Mantém a velocidade constante e perfeitamente alinhada com a direção do disparo.
        body.linearVelocity = moveDirection * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
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
