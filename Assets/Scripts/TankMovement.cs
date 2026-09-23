using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Movimentação de tanque com o sistema oficial de veículos do Unity (<see cref="WheelCollider"/>).
///
/// BASEADO EM:
///  - brihernandez/PhysicsTankExample (github.com/brihernandez/PhysicsTankExample):
///    "three different ways to use Unity's standard WheelCollider to create a driveable tank".
///    Adotamos a variante MAGIC STEERING: tração nas rodas (motorTorque) + curva do tanque
///    aplicada direto no Rigidbody com <c>rigidbody.MoveRotation()</c> ("very controllable and
///    predictable", conforme o README do repositório). A diferenciação pelas esteiras
///    (differential steering) foi descartada pelo próprio autor do repositório porque o atrito
///    lateral dos WheelColliders a impede de funcionar bem.
///  - Manual oficial do Unity (Wheel collider suspension): valores de Spring/Damper devem ser
///    proporcionais à massa do Rigidbody (padrão 1500 kg → spring 35000 / damper 4500; aqui a
///    cena usa massa 10 → spring 235 / damper 30) e a Target Position é o ponto de repouso da
///    roda sob carga.
///
/// POR QUE ISSO RESOLVE O PROBLEMA DA RAMPA:
///  - Antes, o script sobrescrevia rb.linearVelocity a cada FixedUpdate e "brigava" contra o
///    solver do PhysX: ao encostar na rampa, a velocidade reimposta gerava um impulso enorme
///    de normal/atrito no contato dianteiro, cujo torque levantava o traseiro do chão.
///  - Agora o casco não é mais impulsionado por velocidade imposta: 4 WheelColliders com
///    suspensão seguram o tanque, as rodas conversam com o terreno (subem a rampa
///    naturalmente) e o torque do motor só é transferido por contato de roda.
///  - Os colisores das esteiras são desativados no Play (as rodas passam a ser o apoio);
///    os demais colisores do casco (corpo/chapa) continuam ativos para paredes e impactsos.
///
/// FLUXO DE INPUT (igual ao do repositório): quem controla chama <see cref="SetInput"/>
/// no Update (ver <see cref="TankPlayerInput"/>); a aplicação acontece no FixedUpdate.
///
/// O componente deve ficar no mesmo GameObject do Rigidbody (raiz do tanque), eixo Z local
/// para a frente e eixo Y local para cima.
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

    [Header("Tração (rodas motrizes)")]
    [Tooltip("Ganho que transforma o erro de velocidade (alvo − real) em torque de motor por roda. " +
             "Maior = a velocidade real acompanha o alvo de perto.")]
    [SerializeField, Min(0f)] private float motorTorqueGain = 8f;

    [Tooltip("Torque máximo (N·m) por roda motriz. Limita a força de tração total (4 rodas).")]
    [SerializeField, Min(0f)] private float maxMotorTorque = 12f;

    [Header("Rotação (graus/s e graus/s²)")]
    [Tooltip("Velocidade máxima de giro em torno do próprio eixo (Magic Steering).")]
    [SerializeField, Min(0f)] private float maxTurnSpeed = 110f;

    [Tooltip("Taxa com que a velocidade de giro sobe em direção ao alvo.")]
    [SerializeField, Min(0f)] private float turnAcceleration = 240f;

    [Tooltip("Taxa com que a velocidade de giro cai ao soltar o input ou ao inverter o lado do giro.")]
    [SerializeField, Min(0f)] private float turnDeceleration = 360f;

    [Tooltip("Se ativo, inverte o lado do giro enquanto o tanque anda de ré (como um carro). " +
             "Desativado, A/D sempre giram o tanque para o mesmo lado (padrão em jogos de tanque).")]
    [SerializeField] private bool invertSteerWhenReversing = false;

    [Header("Rodas — WheelCollider (setup automático)")]
    [Tooltip("Se ativo e o campo 'Wheels' estiver vazio, cria 4 WheelColliders automaticamente " +
             "aposicionados a partir dos colisores das esteiras (2 por esteira: frente e traseira).")]
    [SerializeField] private bool autoSetupWheels = true;

    [Tooltip("WheelColliders do tanque. Preenchido automaticamente em Play (ou atribua manualmente).")]
    [SerializeField] private WheelCollider[] wheels;

    [Tooltip("Se ativo, desativa os colisores das esteiras ao criar as rodas — as rodas passam a " +
             "ser o apoio no chão. Os colisores do casco (corpo/chapa) continuam ativos.")]
    [SerializeField] private bool disableTrackColliders = true;

    [Tooltip("Distância total de curso da suspensão (m). A roda se move para baixo a partir da âncora.")]
    [SerializeField, Min(0.05f)] private float suspensionDistance = 0.25f;

    [Tooltip("Posição de repouso da roda sob carga ao longo do curso (0 = totalmente estendida, " +
             "1 = totalmente comprimida). Manual do Unity: típico entre 0.3 e 0.7.")]
    [Range(0f, 1f)] [SerializeField] private float suspensionTargetPosition = 0.5f;

    [Tooltip("Rigidez da mola (N/m) POR RODA, proporcional à massa (oficial: massa 1500 → 35000; " +
             "massa 10 → ~233). Aumente se a suspensão afundar demais ao subir a rampa.")]
    [SerializeField, Min(1f)] private float suspensionSpring = 235f;

    [Tooltip("Amortecedor (N·s/m) por roda, proporcional à massa (oficial: massa 1500 → 4500; " +
             "massa 10 → 30). Maior = menos quique.")]
    [SerializeField, Min(0f)] private float suspensionDamper = 30f;

    [Tooltip("Massa de cada roda (kg). Deve ser bem menor que a massa do casco.")]
    [SerializeField, Min(0.1f)] private float wheelMass = 2f;

    [Tooltip("Raio das rodas (m). 0 = automático (metade da altura da esteira).")]
    [SerializeField] private float wheelRadius = 0f;

    [Tooltip("Recuo (m) das rodas em relação às pontas da esteira, para a roda não ficar na borda.")]
    [SerializeField, Min(0f)] private float wheelEndInset = 0.4f;

    [Tooltip("Rigidez do atrito longitudinal (tração). Padrão do Unity = 1; ~2 dá mais aderência " +
             "para subir rampa sem patinar.")]
    [SerializeField, Min(0f)] private float forwardFrictionStiffness = 2f;

    [Tooltip("Rigidez do atrito lateral. Mantenha perto de 1 para o Magic Steering não ser " +
             "contrariado pelo atrito das rodas viradas de lado.")]
    [SerializeField, Min(0f)] private float sidewaysFrictionStiffness = 1f;

    [Header("Chassi")]
    [Tooltip("Opcional: transform filho usado para baixar o centro de massa (mais estável, " +
             "evita tombamento). Deve ser filho direto da raiz do tanque. " +
             "(Padrão do PhysicsTankExample.)")]
    [SerializeField] private Transform centerOfMass;

    /// <summary>Input de aceleração atual, de -1 (ré) a 1 (frente). Capturado no Update.</summary>
    public float ThrottleInput { get; private set; }

    /// <summary>Input de giro atual, de -1 (esquerda) a 1 (direita). Capturado no Update.</summary>
    public float SteerInput { get; private set; }

    /// <summary>Velocidade alvo atual em m/s. Positivo = frente, negativo = ré.</summary>
    public float CurrentSpeed { get; private set; }

    /// <summary>Velocidade de giro alvo em graus/s. Positivo = direita, negativo = esquerda.</summary>
    public float CurrentTurnSpeed { get; private set; }

    /// <summary>Velocidade alvo normalizada entre -1 e 1 (esteiras, som do motor, etc.).</summary>
    public float NormalizedSpeed => CurrentSpeed >= 0f
        ? SafeDivide(CurrentSpeed, maxForwardSpeed)
        : -SafeDivide(-CurrentSpeed, maxReverseSpeed);

    /// <summary>Velocidade de giro alvo normalizada entre -1 e 1.</summary>
    public float NormalizedTurnSpeed => SafeDivide(CurrentTurnSpeed, maxTurnSpeed);

    /// <summary>Verdadeiro quando ao menos uma roda está apoiada no chão.</summary>
    public bool IsGrounded { get; private set; }

    public float MaxForwardSpeed => maxForwardSpeed;
    public float MaxReverseSpeed => maxReverseSpeed;
    public float MaxTurnSpeed => maxTurnSpeed;

    /// <summary>WheelColliders efetivamente em uso (automáticos ou atribuídos).</summary>
    public WheelCollider[] Wheels => wheels;

    private Rigidbody rb;
    private int groundedWheelCount;
    private readonly List<Collider> trackColliders = new List<Collider>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Centro de massa opcional (mesmo recurso do PhysicsTankExample): um ponto baixo
        // reduz a tendência de tombamento.
        if (centerOfMass != null)
        {
            if (centerOfMass.parent == transform)
                rb.centerOfMass = centerOfMass.localPosition;
            else
                Debug.LogWarning($"[{nameof(TankMovement)}] '{centerOfMass.name}' precisa ser filho direto de '{name}' para definir o centro de massa.", this);
        }

        if ((wheels == null || wheels.Length == 0) && autoSetupWheels)
            CreateWheelsFromTracks();

        // Recalcula o tensor de inércia a partir dos colisores (no-ops se já estiver correto;
        // corrige caso o rigidbody esteja usando o placeholder padrão).
        rb.ResetInertiaTensor();

        CacheTrackColliders();
        if (disableTrackColliders && wheels != null && wheels.Length > 0)
        {
            foreach (Collider track in trackColliders)
                track.enabled = false;
        }
    }

    /// <summary>
    /// Define o input do tanque. Deve ser chamado no Update por quem controla
    /// (capturar input no Update, aplicar física no FixedUpdate — padrão do PhysicsTankExample).
    /// </summary>
    /// <param name="throttle">-1 (ré) a 1 (frente). 0 = solta o acelerador.</param>
    /// <param name="steer">-1 (esquerda) a 1 (direita). 0 = não gira.</param>
    public void SetInput(float throttle, float steer)
    {
        ThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
        SteerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    /// <summary>Zera o input e as velocidades imediatamente.</summary>
    public void Stop()
    {
        SetInput(0f, 0f);
        CurrentSpeed = 0f;
        CurrentTurnSpeed = 0f;

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = Vector3.Project(velocity, Vector3.up);
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // --- Velocidade alvo (aceleração / freio / deslize) ---
        float targetSpeed = ThrottleInput >= 0f
            ? ThrottleInput * maxForwardSpeed
            : ThrottleInput * maxReverseSpeed;

        CurrentSpeed = Approach(CurrentSpeed, targetSpeed, acceleration, brakingDeceleration, coastingDeceleration, dt);

        // --- Giro alvo ---
        float steer = SteerInput;
        if (invertSteerWhenReversing && CurrentSpeed < -0.01f)
            steer = -steer;

        CurrentTurnSpeed = Approach(CurrentTurnSpeed, steer * maxTurnSpeed, turnAcceleration, turnDeceleration, turnDeceleration, dt);

        UpdateGroundedState();
        RunPoweredWheels();
        RunMagicRotation(dt);
    }

    /// <summary>
    /// TRAÇÃO (RunPoweredWheels do PhysicsTankExample, refinada):
    /// aplica o mesmo torque de motor em todas as rodas. Em vez de ligar/desligar por
    /// <c>topSpeed</c> (como no repositório), usamos o erro entre <see cref="CurrentSpeed"/>
    /// e a velocidade real — assim as curvas de aceleração/freno/deslize originais do tanque
    /// são respeitadas e a ré funciona. O torque só chega ao chão pelo contato das rodas:
    /// nada de velocidade imposta ao casco.
    /// </summary>
    private void RunPoweredWheels()
    {
        if (wheels == null)
            return;

        // Direção de marcha na horizontal do casco.
        Vector3 driveDirection = transform.forward;
        driveDirection.y = 0f;
        if (driveDirection.sqrMagnitude < 1e-6f)
            driveDirection = transform.forward;
        else
            driveDirection.Normalize();

        float actualSpeed = Vector3.Dot(rb.linearVelocity, driveDirection);
        float torque = Mathf.Clamp((CurrentSpeed - actualSpeed) * motorTorqueGain, -maxMotorTorque, maxMotorTorque);

        foreach (WheelCollider wheel in wheels)
        {
            if (wheel != null)
                wheel.motorTorque = torque;
        }
    }

    /// <summary>
    /// MAGIC STEERING (RunMagicRotation do PhysicsTankExample): gira o próprio Rigidbody com
    /// <c>MoveRotation</c>, independente do atrito das rodas. Conforme a nota do próprio
    /// repositório, a força de giro é reduzida proporcionalmente ao número de rodas no chão,
    /// para o tanque não virar no ar.
    /// </summary>
    private void RunMagicRotation(float dt)
    {
        if (Mathf.Approximately(CurrentTurnSpeed, 0f) || wheels == null || wheels.Length == 0)
            return;

        float groundedFactor = wheels.Length > 0 ? (float)groundedWheelCount / wheels.Length : 0f;
        if (groundedFactor <= 0f)
            return;

        float angle = CurrentTurnSpeed * groundedFactor * dt;
        Quaternion newRotation = transform.rotation * Quaternion.AngleAxis(angle, transform.up);
        rb.MoveRotation(newRotation);
    }

    private void UpdateGroundedState()
    {
        groundedWheelCount = 0;

        if (wheels != null)
        {
            foreach (WheelCollider wheel in wheels)
            {
                if (wheel != null && wheel.GetGroundHit(out _))
                    groundedWheelCount++;
            }
        }

        IsGrounded = groundedWheelCount > 0;
    }

    /// <summary>
    /// Cria 4 WheelColliders (2 por esteira) a partir dos colisores mesh das esteiras:
    /// altura/raio/posição derivam do próprio modelo, então o apoio das rodas cai exatamente
    /// onde as esteiras encostavam no chão (altura de marcha preservada).
    /// Pressupõe que o tanque nasça apoiado no chão (como na cena playground).
    /// </summary>
    private void CreateWheelsFromTracks()
    {
        MeshCollider[] tracks = GetComponentsInChildren<MeshCollider>(true);
        if (tracks.Length == 0)
        {
            Debug.LogError($"[{nameof(TankMovement)}] Nenhum MeshCollider de esteira encontrado em '{name}' — impossível montar as rodas automaticamente.", this);
            return;
        }

        List<WheelCollider> created = new List<WheelCollider>();

        foreach (MeshCollider track in tracks)
        {
            if (track.sharedMesh == null)
                continue;

            Bounds localBounds = GetBoundsInRootLocalSpace(track);
            if (localBounds.size.y < 0.01f)
                continue;

            float radius = wheelRadius > 0.01f ? wheelRadius : localBounds.size.y * 0.5f;
            float inset = Mathf.Min(wheelEndInset, localBounds.size.z * 0.25f);

            // Âncora da suspensão: a roda em repouso sob carga fica suspensionTargetPosition·
            // suspensionDistance abaixo da âncora (manual do Unity). Para o centro da roda ficar
            // no centro da esteira e o pé da roda no pé da esteira:
            //   âncoraY = baseDaEsteira + cursoEmRepouso + raio
            float restLength = suspensionTargetPosition * suspensionDistance;
            float anchorY = localBounds.min.y + restLength + radius;

            float[] zOffsets = { localBounds.max.z - inset, localBounds.min.z + inset };
            string[] zNames = { "Frontal", "Traseira" };

            for (int i = 0; i < zOffsets.Length; i++)
            {
                Vector3 localPosition = new Vector3(localBounds.center.x, anchorY, zOffsets[i]);
                WheelCollider wheel = CreateWheel($"{track.name}_{zNames[i]}", localPosition, radius);
                created.Add(wheel);
            }
        }

        if (created.Count == 0)
        {
            Debug.LogError($"[{nameof(TankMovement)}] Falha ao criar WheelColliders a partir das esteiras.", this);
            return;
        }

        wheels = created.ToArray();
    }

    private WheelCollider CreateWheel(string wheelName, Vector3 localPosition, float radius)
    {
        GameObject wheelObject = new GameObject(wheelName);
        wheelObject.layer = gameObject.layer;
        wheelObject.transform.SetParent(transform, false);
        wheelObject.transform.localPosition = localPosition;
        wheelObject.transform.localRotation = Quaternion.identity;

        WheelCollider wheel = wheelObject.AddComponent<WheelCollider>();
        wheel.mass = wheelMass;
        wheel.radius = radius;
        wheel.suspensionDistance = suspensionDistance;
        wheel.center = Vector3.zero;

        var suspension = wheel.suspensionSpring;
        suspension.spring = suspensionSpring;
        suspension.damper = suspensionDamper;
        suspension.targetPosition = suspensionTargetPosition;
        wheel.suspensionSpring = suspension;

        var forward = wheel.forwardFriction;
        forward.stiffness = forwardFrictionStiffness;
        wheel.forwardFriction = forward;

        var sideways = wheel.sidewaysFriction;
        sideways.stiffness = sidewaysFrictionStiffness;
        wheel.sidewaysFriction = sideways;

        return wheel;
    }

    /// <summary>Calcula os limites (bounds) dos 8 cantos do collider mesh no espaço local da raiz do tanque.</summary>
    private Bounds GetBoundsInRootLocalSpace(MeshCollider track)
    {
        Bounds meshBounds = track.sharedMesh.bounds; // espaço local do collider (MeshCollider não tem offset)
        Bounds rootBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                (i & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                (i & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);

            Vector3 local = transform.InverseTransformPoint(track.transform.TransformPoint(corner));

            if (!hasBounds)
            {
                rootBounds = new Bounds(local, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                rootBounds.Encapsulate(local);
            }
        }

        return rootBounds;
    }

    private void CacheTrackColliders()
    {
        trackColliders.Clear();
        foreach (MeshCollider track in GetComponentsInChildren<MeshCollider>(true))
            trackColliders.Add(track);
    }

    /// <summary>
    /// Move <paramref name="current"/> em direção a <paramref name="target"/> usando a taxa adequada:
    ///  - sem input (alvo zero) ou reduzindo a intensidade no mesmo sentido: <paramref name="coastRate"/>;
    ///  - input no sentido oposto ao movimento atual: <paramref name="brakeRate"/>;
    ///  - aumentando a intensidade no mesmo sentido: <paramref name="accelRate"/>.
    /// </summary>
    private static float Approach(float current, float target, float accelRate, float brakeRate, float coastRate, float dt)
    {
        bool noInput = Mathf.Approximately(target, 0f);
        bool opposingDirection = !noInput && current != 0f && Mathf.Sign(target) != Mathf.Sign(current);

        if (opposingDirection)
            return Mathf.MoveTowards(current, 0f, brakeRate * dt);

        bool speedingUp = !noInput && Mathf.Abs(target) > Mathf.Abs(current);
        float rate = speedingUp ? accelRate : coastRate;

        return Mathf.MoveTowards(current, target, rate * dt);
    }

    private static float SafeDivide(float a, float b) => b > Mathf.Epsilon ? a / b : 0f;
}
