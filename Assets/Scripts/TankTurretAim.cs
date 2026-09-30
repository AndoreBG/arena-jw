using UnityEngine;

/// <summary>
/// Faz a torre acompanhar horizontalmente a câmera e inclina o cano conforme a
/// direção vertical do olhar. O casco continua independente da orientação da câmera.
/// </summary>
[DefaultExecutionOrder(100)] // Executa depois da câmera orbital atualizar seu Transform.
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Turret Aim")]
public sealed class TankTurretAim : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Transform que gira a torre e todos os seus filhos.")]
    [SerializeField] private Transform turret;

    [Tooltip("Transform cuja frente representa a direção do cano. Normalmente, o Trabuco.")]
    [SerializeField] private Transform aimReference;

    [Tooltip("Câmera que define a direção da torre. Se vazio, usa Camera.main.")]
    [SerializeField] private Camera aimCamera;

    [Header("Pivot do Trabuco")]
    [Tooltip("Collider usado para localizar a extremidade traseira do Trabuco, onde ele se conecta à torre.")]
    [SerializeField] private BoxCollider barrelBounds;

    [Tooltip("Ajuste fino do pivot no espaço local do Trabuco.")]
    [SerializeField] private Vector3 connectionOffset;

    [Header("Movimento")]
    [Tooltip("Velocidade horizontal máxima da torre em graus por segundo. Zero faz o giro instantâneo.")]
    [SerializeField, Min(0f)] private float rotationSpeed = 240f;

    [Tooltip("Velocidade vertical máxima do cano em graus por segundo. Zero faz o movimento instantâneo.")]
    [SerializeField, Min(0f)] private float elevationSpeed = 120f;

    [Tooltip("Inclinação mínima e máxima do cano. Valores positivos apontam para baixo, como o pitch da câmera.")]
    [SerializeField] private Vector2 elevationLimits = new Vector2(-15f, 75f);

    private Vector3 connectionPointInBarrelSpace;
    private Vector3 connectionPointInTurretSpace;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (barrelBounds == null && aimReference != null)
            barrelBounds = aimReference.GetComponent<BoxCollider>();

        if (turret == null || aimReference == null || aimCamera == null || barrelBounds == null)
        {
            Debug.LogError(
                $"[{nameof(TankTurretAim)}] Configure Turret, Aim Reference, Barrel Bounds e uma câmera válida em '{name}'.",
                this);
            enabled = false;
            return;
        }

        // O collider acompanha o comprimento do cano no eixo Z. A extremidade -Z é a
        // parte traseira, conectada à torre. Guardamos esse ponto no espaço da torre
        // para que ele continue fixo mesmo enquanto o cano inclina.
        connectionPointInBarrelSpace = barrelBounds.center
            - Vector3.forward * (barrelBounds.size.z * 0.5f)
            + connectionOffset;
        Vector3 connectionInWorldSpace = aimReference.TransformPoint(connectionPointInBarrelSpace);
        connectionPointInTurretSpace = turret.InverseTransformPoint(connectionInWorldSpace);
    }

    private void LateUpdate()
    {
        if (turret == null || aimReference == null || aimCamera == null)
            return;

        Vector3 rotationAxis = transform.up;
        Vector3 cameraForward = aimCamera.transform.forward;
        Vector3 desiredFlatDirection = Vector3.ProjectOnPlane(cameraForward, rotationAxis);
        Vector3 currentFlatDirection = Vector3.ProjectOnPlane(aimReference.forward, rotationAxis);

        // Evita rotações indefinidas quando a câmera ou o cano estão quase verticais.
        if (desiredFlatDirection.sqrMagnitude < 0.0001f || currentFlatDirection.sqrMagnitude < 0.0001f)
            return;

        desiredFlatDirection.Normalize();
        currentFlatDirection.Normalize();

        // Giro horizontal da torre.
        float yawAngle = Vector3.SignedAngle(currentFlatDirection, desiredFlatDirection, rotationAxis);
        Quaternion targetTurretRotation = Quaternion.AngleAxis(yawAngle, rotationAxis) * turret.rotation;

        turret.rotation = rotationSpeed <= 0f
            ? targetTurretRotation
            : Quaternion.RotateTowards(turret.rotation, targetTurretRotation, rotationSpeed * Time.deltaTime);

        // Inclinação vertical do Trabuco. Recalcula os eixos após o giro da torre,
        // pois aimReference é filho dela.
        currentFlatDirection = Vector3.ProjectOnPlane(aimReference.forward, rotationAxis).normalized;
        Vector3 pitchAxis = Vector3.Cross(rotationAxis, currentFlatDirection).normalized;
        Vector3 cameraPitchAxis = Vector3.Cross(rotationAxis, desiredFlatDirection).normalized;

        float cameraElevation = Vector3.SignedAngle(
            desiredFlatDirection,
            cameraForward,
            cameraPitchAxis);
        float targetElevation = Mathf.Clamp(cameraElevation, elevationLimits.x, elevationLimits.y);
        Vector3 targetBarrelDirection =
            Quaternion.AngleAxis(targetElevation, pitchAxis) * currentFlatDirection;

        float pitchDelta = Vector3.SignedAngle(aimReference.forward, targetBarrelDirection, pitchAxis);
        Quaternion targetBarrelRotation =
            Quaternion.AngleAxis(pitchDelta, pitchAxis) * aimReference.rotation;

        Quaternion nextBarrelRotation = elevationSpeed <= 0f
            ? targetBarrelRotation
            : Quaternion.RotateTowards(
                aimReference.rotation,
                targetBarrelRotation,
                elevationSpeed * Time.deltaTime);

        RotateBarrelAroundConnection(nextBarrelRotation);
    }

    /// <summary>
    /// Aplica a rotação e compensa a posição do Transform para que a extremidade traseira
    /// permaneça presa à torre. Isso equivale a usar um GameObject-pivot na conexão, sem
    /// alterar a hierarquia nem o pivot central importado do mesh.
    /// </summary>
    private void RotateBarrelAroundConnection(Quaternion nextRotation)
    {
        Vector3 pivot = turret.TransformPoint(connectionPointInTurretSpace);

        aimReference.rotation = nextRotation;

        // Corrige a posição depois da rotação. Calcular o ponto novamente com TransformPoint
        // também mantém a conexão exata sob as escalas não uniformes do modelo importado.
        Vector3 displacedConnection = aimReference.TransformPoint(connectionPointInBarrelSpace);
        aimReference.position += pivot - displacedConnection;
    }

    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        elevationSpeed = Mathf.Max(0f, elevationSpeed);

        if (elevationLimits.x > elevationLimits.y)
        {
            float minimum = elevationLimits.y;
            elevationLimits.y = elevationLimits.x;
            elevationLimits.x = minimum;
        }

        if (aimReference == null && turret != null && turret.childCount > 0)
            aimReference = turret.GetChild(0);

        if (barrelBounds == null && aimReference != null)
            barrelBounds = aimReference.GetComponent<BoxCollider>();
    }
}
