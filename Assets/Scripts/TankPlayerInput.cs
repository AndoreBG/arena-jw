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

    [Header("Configurações do Disparo")]
    [Tooltip("Tempo de intervalo mínimo entre disparos em segundos (cooldown).")]
    [SerializeField, Min(0f)] private float fireRate = 0.2f;

    [Tooltip("Duração do clarão da luz (FX_TiroBrilho) a cada disparo.")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.05f;

    private InputAction moveAction;
    private InputAction fireAction;
    private float nextFireTime;
    private Coroutine flashCoroutine;

    private void Reset()
    {
        movement = GetComponent<TankMovement>();
        FindShootReferences();
    }

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<TankMovement>();

        FindShootReferences();

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
    /// Dispara o efeito de partículas FX_Tiro e ativa o clarão da luz temporariamente.
    /// Pode ser chamado externamente por outros scripts ou botões de UI.
    /// </summary>
    public bool TryFire()
    {
        if (Time.time < nextFireTime)
            return false;

        nextFireTime = Time.time + fireRate;

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
    }
}
