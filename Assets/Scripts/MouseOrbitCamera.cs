using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Câmera orbital em terceira pessoa controlada pelo mouse.
///
/// A órbita usa eixos do mundo, não a rotação do alvo. Assim, girar a câmera não altera
/// a referência usada pelo TankMovement: W/S continuam seguindo transform.forward do tanque
/// e A/D continuam girando o próprio tanque.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Camera/Mouse Orbit Camera")]
public sealed class MouseOrbitCamera : MonoBehaviour
{
    [Header("Alvo")]
    [Tooltip("Objeto acompanhado pela câmera. Na cena playground, é a raiz do Tanque.")]
    [SerializeField] private Transform target;

    [Tooltip("Altura do ponto observado, no espaço do mundo.")]
    [SerializeField] private float targetHeight = 2f;

    [Header("Órbita")]
    [SerializeField, Min(0.1f)] private float distance = 6.5f;

    [Tooltip("Graus de rotação por pixel de movimento do mouse.")]
    [SerializeField, Min(0.001f)] private float sensitivity = 0.12f;

    [SerializeField] private float initialYaw = 0f;
    [SerializeField] private float initialPitch = 28f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-15f, 75f);

    [Header("Suavização")]
    [Tooltip("Rapidez com que a câmera acompanha a posição do alvo. Zero remove a suavização.")]
    [SerializeField, Min(0f)] private float followSharpness = 15f;

    [Tooltip("Trava e esconde o cursor enquanto a câmera está sendo controlada.")]
    [SerializeField] private bool lockCursorOnStart = true;

    private float yaw;
    private float pitch;
    private Vector3 smoothedPivot;
    private bool initialized;

    private void Awake()
    {
        yaw = initialYaw;
        pitch = Mathf.Clamp(initialPitch, pitchLimits.x, pitchLimits.y);

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (target == null)
        {
            Debug.LogError($"[{nameof(MouseOrbitCamera)}] Nenhum alvo foi atribuído.", this);
            enabled = false;
            return;
        }

        smoothedPivot = GetTargetPivot();
        initialized = true;
    }

    private void OnEnable()
    {
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    private void OnDisable()
    {
        if (lockCursorOnStart)
            SetCursorLocked(false);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        // Escape libera o cursor; clique esquerdo volta a controlar a câmera.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);
        else if (lockCursorOnStart && mouse.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        if (Cursor.lockState != CursorLockMode.Locked && lockCursorOnStart)
            return;

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * sensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * sensitivity, pitchLimits.x, pitchLimits.y);
    }

    private void LateUpdate()
    {
        if (!initialized || target == null)
            return;

        Vector3 desiredPivot = GetTargetPivot();
        if (followSharpness <= 0f)
        {
            smoothedPivot = desiredPivot;
        }
        else
        {
            // Suavização independente do frame rate.
            float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            smoothedPivot = Vector3.Lerp(smoothedPivot, desiredPivot, blend);
        }

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(
            smoothedPivot - orbit * Vector3.forward * distance,
            orbit);
    }

    private Vector3 GetTargetPivot()
    {
        // targetHeight é mundial de propósito: a câmera não inclina junto com o tanque.
        return target.position + Vector3.up * targetHeight;
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0.1f, distance);
        sensitivity = Mathf.Max(0.001f, sensitivity);
        followSharpness = Mathf.Max(0f, followSharpness);

        if (pitchLimits.x > pitchLimits.y)
            (pitchLimits.x, pitchLimits.y) = (pitchLimits.y, pitchLimits.x);

        initialPitch = Mathf.Clamp(initialPitch, pitchLimits.x, pitchLimits.y);
    }
}
