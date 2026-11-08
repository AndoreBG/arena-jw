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
    [Header("Mouse")]
    [SerializeField, Min(0.001f)] private float sensitivity = 0.12f;

    [Header("Controle")]
    [Tooltip("Velocidade da câmera no stick direito, em graus por segundo com o stick no máximo.")]
    [SerializeField, Min(1f)] private float sensibilidadeDoControle = 180f;

    [Tooltip("Quanto o stick precisa sair do centro para a câmera começar a andar.")]
    [SerializeField, Range(0f, 0.9f)] private float zonaMortaDoAnalógico = 0.18f;

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
        // Escape agora é o atalho de "segure para voltar ao menu" (AtalhoParaOMenu),
        // então não solta mais o cursor: as duas coisas brigariam pela mesma tecla.
        // Clicar com o botão esquerdo volta a controlar a câmera.
        Mouse mouse = Mouse.current;
        if (mouse != null && lockCursorOnStart && mouse.leftButton.wasPressedThisFrame && ArenaManager.IsPlaying)
            SetCursorLocked(true); // Não captura o cursor com o Game Over aberto (os botões precisam dele).

        // Mouse e controle convivem: cada um responde pelo seu próprio sinal, sem precisar
        // escolher um "modo". Amarrar o controle ao cursor solto era um erro: em partida o
        // cursor fica travado, então o stick direito NUNCA era lido.
        bool cursorTravado = Cursor.lockState == CursorLockMode.Locked;
        if (mouse != null && cursorTravado) GirarComMouse(mouse);
        GirarComControle();
    }

    private void GirarComMouse(Mouse mouse)
    {
        Girar(mouse.delta.ReadValue(), sensitivity);
    }

    /// <summary>
    /// Stick direito (ou canhão/segundo analógico, no controle que tiver). O analógico
    /// entrega um valor de -1 a 1, então precisa do delta do frame para virar velocidade.
    /// </summary>
    private void GirarComControle()
    {
        Gamepad controle = Gamepad.current;
        if (controle == null) return;

        Vector2 leitura = controle.rightStick.ReadValue();

        // Zona morta: sem isso a câmera treme e anda sozinha com o analógico parado.
        if (leitura.magnitude < zonaMortaDoAnalógico) return;

        // O stick entrega -1..1 (uma velocidade), então vira delta do frame com dt.
        // Escala = sensibilidadeDoControle, em graus por segundo.
        Girar(leitura * Time.unscaledDeltaTime, sensibilidadeDoControle);
    }

    /// <summary>
    /// Aplica o giro. A sensibilidade entra aqui porque mouse (delta em pixels, ~0,12) e
    /// stick (delta em graus por segundo, ~180) estão em escalas completamente diferentes.
    /// </summary>
    private void Girar(Vector2 delta, float escala)
    {
        yaw += delta.x * escala;
        pitch = Mathf.Clamp(pitch - delta.y * escala, pitchLimits.x, pitchLimits.y);
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
