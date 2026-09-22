using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lê o input do jogador pelo Input System e repassa para o <see cref="TankMovement"/>.
///
/// Espera, no asset de Input Actions, um mapa (padrão "Tank") com uma ação (padrão "Move")
/// do tipo Value/Vector2:
///   - Y  = frente (+) / trás (-)            → W/S, setas cima/baixo, analógico esquerdo
///   - X  = girar direita (+) / esquerda (-) → A/D, setas esquerda/direita, analógico esquerdo
/// </summary>
[RequireComponent(typeof(TankMovement))]
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Player Input")]
public class TankPlayerInput : MonoBehaviour
{
    [Tooltip("Asset de Input Actions (ex.: TankInputActions) que contém o mapa e a ação de movimento.")]
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Nome do Action Map dentro do asset.")]
    [SerializeField] private string actionMapName = "Tank";

    [Tooltip("Nome da ação Vector2 de movimento dentro do Action Map.")]
    [SerializeField] private string moveActionName = "Move";

    [Tooltip("Componente de movimentação que receberá o input. Se vazio, usa o do próprio GameObject.")]
    [SerializeField] private TankMovement movement;

    private InputAction moveAction;

    private void Reset()
    {
        movement = GetComponent<TankMovement>();
    }

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<TankMovement>();

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
    }

    private void OnEnable()
    {
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();

        // Evita que o tanque continue andando com o último input recebido.
        if (movement != null)
            movement.SetInput(0f, 0f);
    }

    private void Update()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();
        movement.SetInput(throttle: move.y, steer: move.x);
    }
}
