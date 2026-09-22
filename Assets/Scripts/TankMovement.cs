using UnityEngine;

/// <summary>
/// Movimentação de tanque baseada em física (Rigidbody).
///
/// Funcionalidades:
///  - Andar para frente e para trás (ao longo de <c>transform.forward</c>).
///  - Girar em torno do próprio eixo, para a esquerda e para a direita (em torno de <c>transform.up</c>).
///  - Todos os movimentos têm aceleração, frenagem e desaceleração configuráveis.
///
/// Este componente NÃO lê input diretamente. Quem controla o tanque (jogador, IA, rede...)
/// chama <see cref="SetInput"/> a cada frame com os valores de aceleração (throttle) e giro (steer).
/// Veja <see cref="TankPlayerInput"/> para a versão controlada pelo jogador.
///
/// O componente deve ficar no mesmo GameObject do Rigidbody (a raiz do tanque), com o
/// eixo Z local apontando para a frente do tanque e o eixo Y local para cima.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Movement")]
public class TankMovement : MonoBehaviour
{
    [Header("Movimento linear (m/s e m/s²)")]
    [Tooltip("Velocidade máxima andando para frente.")]
    [SerializeField, Min(0f)] private float maxForwardSpeed = 8f;

    [Tooltip("Velocidade máxima andando de ré.")]
    [SerializeField, Min(0f)] private float maxReverseSpeed = 4f;

    [Tooltip("Taxa com que a velocidade sobe em direção à velocidade alvo.")]
    [SerializeField, Min(0f)] private float acceleration = 5f;

    [Tooltip("Taxa com que a velocidade cai quando o input é oposto ao movimento atual (frear para inverter o sentido).")]
    [SerializeField, Min(0f)] private float brakingDeceleration = 12f;

    [Tooltip("Taxa com que a velocidade cai quando não há input (o tanque desliza até parar).")]
    [SerializeField, Min(0f)] private float coastingDeceleration = 6f;

    [Header("Rotação (graus/s e graus/s²)")]
    [Tooltip("Velocidade máxima de giro em torno do próprio eixo.")]
    [SerializeField, Min(0f)] private float maxTurnSpeed = 110f;

    [Tooltip("Taxa com que a velocidade de giro sobe em direção ao alvo.")]
    [SerializeField, Min(0f)] private float turnAcceleration = 240f;

    [Tooltip("Taxa com que a velocidade de giro cai ao soltar o input ou ao inverter o lado do giro.")]
    [SerializeField, Min(0f)] private float turnDeceleration = 360f;

    [Tooltip("Se ativo, inverte o lado do giro enquanto o tanque anda de ré (como um carro). " +
             "Desativado, A/D sempre giram o tanque para o mesmo lado (padrão em jogos de tanque).")]
    [SerializeField] private bool invertSteerWhenReversing = false;

    /// <summary>Input de aceleração atual, de -1 (ré) a 1 (frente).</summary>
    public float ThrottleInput { get; private set; }

    /// <summary>Input de giro atual, de -1 (esquerda) a 1 (direita).</summary>
    public float SteerInput { get; private set; }

    /// <summary>Velocidade linear atual em m/s. Positivo = para frente, negativo = ré.</summary>
    public float CurrentSpeed { get; private set; }

    /// <summary>Velocidade de giro atual em graus/s. Positivo = horário (direita), negativo = anti-horário (esquerda).</summary>
    public float CurrentTurnSpeed { get; private set; }

    /// <summary>Velocidade linear atual normalizada entre -1 e 1 (útil para animar esteiras, som do motor, etc.).</summary>
    public float NormalizedSpeed => CurrentSpeed >= 0f
        ? SafeDivide(CurrentSpeed, maxForwardSpeed)
        : -SafeDivide(-CurrentSpeed, maxReverseSpeed);

    /// <summary>Velocidade de giro atual normalizada entre -1 e 1.</summary>
    public float NormalizedTurnSpeed => SafeDivide(CurrentTurnSpeed, maxTurnSpeed);

    public float MaxForwardSpeed => maxForwardSpeed;
    public float MaxReverseSpeed => maxReverseSpeed;
    public float MaxTurnSpeed => maxTurnSpeed;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // O PhysX limita a velocidade angular (padrão: 7 rad/s ≈ 400°/s).
        // Garante que esse limite não corte a velocidade de giro configurada.
        rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, maxTurnSpeed * Mathf.Deg2Rad);
    }

    /// <summary>
    /// Define o input do tanque. Deve ser chamado continuamente por quem controla o tanque.
    /// </summary>
    /// <param name="throttle">-1 (ré) a 1 (frente). 0 = solta o acelerador.</param>
    /// <param name="steer">-1 (girar para a esquerda) a 1 (girar para a direita). 0 = não gira.</param>
    public void SetInput(float throttle, float steer)
    {
        ThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
        SteerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    /// <summary>Zera o input e as velocidades imediatamente (sem desaceleração).</summary>
    public void Stop()
    {
        SetInput(0f, 0f);
        CurrentSpeed = 0f;
        CurrentTurnSpeed = 0f;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // --- Movimento linear: frente / trás ---
        float targetSpeed = ThrottleInput >= 0f
            ? ThrottleInput * maxForwardSpeed
            : ThrottleInput * maxReverseSpeed;

        CurrentSpeed = Approach(CurrentSpeed, targetSpeed, acceleration, brakingDeceleration, coastingDeceleration, dt);

        // --- Rotação: esquerda / direita ---
        float steer = SteerInput;
        if (invertSteerWhenReversing && CurrentSpeed < -0.01f)
            steer = -steer;

        float targetTurnSpeed = steer * maxTurnSpeed;
        CurrentTurnSpeed = Approach(CurrentTurnSpeed, targetTurnSpeed, turnAcceleration, turnDeceleration, turnDeceleration, dt);

        ApplyToRigidbody();
    }

    private void ApplyToRigidbody()
    {
        Vector3 forward = transform.forward;
        Vector3 up = transform.up;

        // Substitui a velocidade no plano do tanque pela velocidade calculada, mas mantém a
        // componente vertical para que gravidade, quedas e colisões continuem funcionando.
        Vector3 verticalVelocity = Vector3.Project(rb.linearVelocity, up);
        rb.linearVelocity = forward * CurrentSpeed + verticalVelocity;

        // Mesma ideia para a rotação: controlamos só o giro em torno do eixo vertical do tanque
        // e deixamos a física cuidar do resto (por exemplo, inclinar ao subir uma rampa).
        Vector3 angularVelocity = rb.angularVelocity;
        angularVelocity -= Vector3.Project(angularVelocity, up);
        rb.angularVelocity = angularVelocity + up * (CurrentTurnSpeed * Mathf.Deg2Rad);
    }

    /// <summary>
    /// Move <paramref name="current"/> em direção a <paramref name="target"/> usando a taxa adequada:
    ///  - sem input (alvo zero) ou reduzindo a intensidade no mesmo sentido: <paramref name="coastRate"/>;
    ///  - input no sentido oposto ao movimento atual: <paramref name="brakeRate"/> (freia até zero primeiro,
    ///    e só então acelera no novo sentido);
    ///  - aumentando a intensidade no mesmo sentido: <paramref name="accelRate"/>.
    /// </summary>
    private static float Approach(float current, float target, float accelRate, float brakeRate, float coastRate, float dt)
    {
        bool noInput = Mathf.Approximately(target, 0f);
        bool opposingDirection = !noInput && current != 0f && Mathf.Sign(target) != Mathf.Sign(current);

        if (opposingDirection)
        {
            // Freia até parar; no próximo passo o sinal já é o mesmo e passa a acelerar.
            return Mathf.MoveTowards(current, 0f, brakeRate * dt);
        }

        bool speedingUp = !noInput && Mathf.Abs(target) > Mathf.Abs(current);
        float rate = speedingUp ? accelRate : coastRate;

        return Mathf.MoveTowards(current, target, rate * dt);
    }

    private static float SafeDivide(float a, float b) => b > Mathf.Epsilon ? a / b : 0f;

    private void OnValidate()
    {
        if (rb != null)
            rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, maxTurnSpeed * Mathf.Deg2Rad);
    }
}
