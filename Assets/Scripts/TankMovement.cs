using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Movimentação de tanque com WheelCollider — versão simplificada.
///
/// Baseada em dois sistemas estabelecidos:
///  - brihernandez/PhysicsTankExample: tração por motorTorque nas rodas e "Magic Steering"
///    (giro do tanque via rigidbody.MoveRotation, previsível e independente do atrito lateral).
///  - Manual oficial do Unity (Wheel collider suspension): spring/damper proporcionais à massa
///    e geometria da suspensão (targetPosition = repouso sob carga).
///
/// Como funciona: em Play são criados 4 WheelColliders a partir dos bounds das esteiras
/// (os colisores das esteiras são desativados — as rodas passam a ser o apoio). O torque do
/// motor só chega ao chão pelo contato das rodas; nenhuma velocidade é imposta ao casco,
/// então a reação da rampa é preservada e o tanque sobe sem levantar o traseiro.
///
/// Entrada: chame <see cref="SetInput"/> no Update (ver <see cref="TankPlayerInput"/>);
/// a física é aplicada no FixedUpdate. Raiz do tanque: Z = frente, Y = cima.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Movement")]
public class TankMovement : MonoBehaviour
{
    // --- Configuração estrutural das rodas (não costuma mudar) ---
    // Spring/damper seguem a proporção oficial do Unity: massa 1500 → 35000/4500;
    // esta cena tem massa 10 → ~235/30. Se mudar a massa, ajuste na mesma proporção.
    private const float SuspensionDistance = 0.25f;
    private const float SuspensionTarget = 0.5f;
    private const float WheelMass = 2f;
    private const float WheelEndInset = 0.4f;
    private const float ForwardFrictionStiffness = 2f; // mais aderência para subir rampa
    private const float SidewaysFrictionStiffness = 1f;

    [Header("Velocidade (m/s e m/s²)")]
    [Tooltip("Velocidade máxima para frente.")]
    [SerializeField, Min(0f)] private float maxForwardSpeed = 8f;

    [Tooltip("Velocidade máxima de ré.")]
    [SerializeField, Min(0f)] private float maxReverseSpeed = 4f;

    [Tooltip("Taxa de aceleração até a velocidade alvo.")]
    [SerializeField, Min(0f)] private float acceleration = 5f;

    [Tooltip("Taxa de frenagem ao inverter o sentido.")]
    [SerializeField, Min(0f)] private float brakingDeceleration = 12f;

    [Tooltip("Taxa de desaceleração sem input.")]
    [SerializeField, Min(0f)] private float coastingDeceleration = 6f;

    [Header("Tração (rodas)")]
    [Tooltip("Torque (N·m) por roda por unidade de erro de velocidade.")]
    [SerializeField, Min(0f)] private float motorTorqueGain = 8f;

    [Tooltip("Torque máximo (N·m) por roda.")]
    [SerializeField, Min(0f)] private float maxMotorTorque = 12f;

    [Header("Giro (Magic Steering)")]
    [Tooltip("Velocidade máxima de giro em graus/s.")]
    [SerializeField, Min(0f)] private float maxTurnSpeed = 110f;

    [Tooltip("Taxa de aceleração/desaceleração do giro (graus/s²).")]
    [SerializeField, Min(0f)] private float turnAcceleration = 240f;

    [Tooltip("Inverte o giro enquanto anda de ré (padrão de carro). Desligado = tanque (A/D sempre o mesmo lado).")]
    [SerializeField] private bool invertSteerWhenReversing = false;

    [Header("Suspensão")]
    [Tooltip("Rigidez da mola por roda (N/m). Proporcional à massa — veja a constante no código.")]
    [SerializeField, Min(1f)] private float suspensionSpring = 235f;

    [Tooltip("Amortecedor por roda (N·s/m). Proporcional à massa — veja a constante no código.")]
    [SerializeField, Min(0f)] private float suspensionDamper = 30f;

    [Header("Chassi")]
    [Tooltip("Filho direto usado como centro de massa (baixo = mais estável).")]
    [SerializeField] private Transform centerOfMass;

    // --- API ---
    public float ThrottleInput { get; private set; }
    public float SteerInput { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float CurrentTurnSpeed { get; private set; }

    public float NormalizedSpeed => CurrentSpeed >= 0f
        ? SafeDivide(CurrentSpeed, maxForwardSpeed)
        : -SafeDivide(-CurrentSpeed, maxReverseSpeed);

    public float NormalizedTurnSpeed => SafeDivide(CurrentTurnSpeed, maxTurnSpeed);

    public float MaxForwardSpeed => maxForwardSpeed;
    public float MaxReverseSpeed => maxReverseSpeed;
    public float MaxTurnSpeed => maxTurnSpeed;

    private Rigidbody rb;
    private WheelCollider[] wheels; // criadas em Awake

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (centerOfMass != null)
        {
            if (centerOfMass.parent == transform)
                rb.centerOfMass = centerOfMass.localPosition;
            else
                Debug.LogWarning($"[{nameof(TankMovement)}] '{centerOfMass.name}' precisa ser filho direto de '{name}'.", this);
        }

        CreateWheels();

        // Inércia calculada a partir dos colisores (corrige o tensor padrão, se for o caso).
        rb.ResetInertiaTensor();
    }

    /// <summary>Chame no Update: throttle -1..1, steer -1..1.</summary>
    public void SetInput(float throttle, float steer)
    {
        ThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
        SteerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    /// <summary>Zera input e velocidades imediatamente.</summary>
    public void Stop()
    {
        SetInput(0f, 0f);
        CurrentSpeed = 0f;
        CurrentTurnSpeed = 0f;

        if (rb != null)
            rb.linearVelocity = Vector3.Project(rb.linearVelocity, Vector3.up);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Velocidade alvo (acelera / freia / desliza)
        float targetSpeed = ThrottleInput >= 0f
            ? ThrottleInput * maxForwardSpeed
            : ThrottleInput * maxReverseSpeed;

        CurrentSpeed = Approach(CurrentSpeed, targetSpeed, acceleration, brakingDeceleration, coastingDeceleration, dt);

        // Giro alvo (desaceleração = 1,5× a aceleração — mantém a relação original 240/360)
        float steer = SteerInput;
        if (invertSteerWhenReversing && CurrentSpeed < -0.01f)
            steer = -steer;

        float turnDeceleration = turnAcceleration * 1.5f;
        CurrentTurnSpeed = Approach(CurrentTurnSpeed, steer * maxTurnSpeed,
            turnAcceleration, turnDeceleration, turnDeceleration, dt);

        RunPoweredWheels();
        RunMagicRotation(dt);
    }

    /// <summary>Tração: torque igual em todas as rodas, proporcional ao erro de velocidade.</summary>
    private void RunPoweredWheels()
    {
        if (wheels == null)
            return;

        Vector3 driveDirection = transform.forward;
        driveDirection.y = 0f;
        driveDirection = driveDirection.sqrMagnitude > 1e-6f ? driveDirection.normalized : transform.forward;

        float actualSpeed = Vector3.Dot(rb.linearVelocity, driveDirection);
        float torque = Mathf.Clamp((CurrentSpeed - actualSpeed) * motorTorqueGain, -maxMotorTorque, maxMotorTorque);

        foreach (WheelCollider wheel in wheels)
            wheel.motorTorque = torque;
    }

    /// <summary>Magic Steering: gira o Rigidbody com MoveRotation (não briga com o atrito das rodas).</summary>
    private void RunMagicRotation(float dt)
    {
        if (wheels == null || Mathf.Approximately(CurrentTurnSpeed, 0f))
            return;

        Quaternion newRotation = transform.rotation * Quaternion.AngleAxis(CurrentTurnSpeed * dt, transform.up);
        rb.MoveRotation(newRotation);
    }

    /// <summary>
    /// Cria 4 WheelColliders (frente/traseira em cada esteira) na altura exata em que as
    /// esteiras encostavam no chão e desativa os colisores das esteiras.
    /// Pressupõe o tanque nascendo apoiado no chão (como na playground).
    /// </summary>
    private void CreateWheels()
    {
        MeshCollider[] tracks = GetComponentsInChildren<MeshCollider>(true);
        List<WheelCollider> created = new List<WheelCollider>();

        foreach (MeshCollider track in tracks)
        {
            if (track.sharedMesh == null)
                continue;

            Bounds bounds = GetRootLocalBounds(track);
            if (bounds.size.y < 0.01f)
                continue;

            float radius = bounds.size.y * 0.5f;
            float inset = Mathf.Min(WheelEndInset, bounds.size.z * 0.25f);

            // âncora = base da esteira + comprimento em repouso da suspensão + raio
            float anchorY = bounds.min.y + SuspensionTarget * SuspensionDistance + radius;

            created.Add(CreateWheel($"{track.name}_Frontal",
                new Vector3(bounds.center.x, anchorY, bounds.max.z - inset), radius));
            created.Add(CreateWheel($"{track.name}_Traseira",
                new Vector3(bounds.center.x, anchorY, bounds.min.z + inset), radius));
        }

        if (created.Count == 0)
        {
            Debug.LogError($"[{nameof(TankMovement)}] Nenhum MeshCollider de esteira encontrado em '{name}'.", this);
            return;
        }

        wheels = created.ToArray();

        // As rodas passam a ser o apoio; corpo/chapa continuam ativos para paredes.
        foreach (MeshCollider track in tracks)
            track.enabled = false;
    }

    private WheelCollider CreateWheel(string wheelName, Vector3 localPosition, float radius)
    {
        GameObject wheelObject = new GameObject(wheelName);
        wheelObject.layer = gameObject.layer;
        wheelObject.transform.SetParent(transform, false);
        wheelObject.transform.localPosition = localPosition;

        WheelCollider wheel = wheelObject.AddComponent<WheelCollider>();
        wheel.mass = WheelMass;
        wheel.radius = radius;
        wheel.suspensionDistance = SuspensionDistance;

        var suspension = wheel.suspensionSpring;
        suspension.spring = suspensionSpring;
        suspension.damper = suspensionDamper;
        suspension.targetPosition = SuspensionTarget;
        wheel.suspensionSpring = suspension;

        var forward = wheel.forwardFriction;
        forward.stiffness = ForwardFrictionStiffness;
        wheel.forwardFriction = forward;

        var sideways = wheel.sidewaysFriction;
        sideways.stiffness = SidewaysFrictionStiffness;
        wheel.sidewaysFriction = sideways;

        return wheel;
    }

    /// <summary>Bounds do mesh do collider no espaço local da raiz do tanque.</summary>
    private Bounds GetRootLocalBounds(MeshCollider track)
    {
        Bounds meshBounds = track.sharedMesh.bounds;
        Bounds rootBounds = default;
        bool first = true;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                (i & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                (i & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);

            Vector3 local = transform.InverseTransformPoint(track.transform.TransformPoint(corner));

            if (first)
            {
                rootBounds = new Bounds(local, Vector3.zero);
                first = false;
            }
            else
            {
                rootBounds.Encapsulate(local);
            }
        }

        return rootBounds;
    }

    private static float Approach(float current, float target, float accelRate, float brakeRate, float coastRate, float dt)
    {
        bool noInput = Mathf.Approximately(target, 0f);
        bool opposing = !noInput && current != 0f && Mathf.Sign(target) != Mathf.Sign(current);

        if (opposing)
            return Mathf.MoveTowards(current, 0f, brakeRate * dt);

        float rate = !noInput && Mathf.Abs(target) > Mathf.Abs(current) ? accelRate : coastRate;
        return Mathf.MoveTowards(current, target, rate * dt);
    }

    private static float SafeDivide(float a, float b) => b > Mathf.Epsilon ? a / b : 0f;
}
