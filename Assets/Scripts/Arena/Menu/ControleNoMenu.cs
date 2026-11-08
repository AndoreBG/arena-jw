using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Deixa o menu inteiro navegável no controle.
///
/// O Unity só navega com D-pad/stick quando existe um objeto selecionado no
/// <see cref="EventSystem"/> — e a cena abre sem nenhum. Este componente:
///
///  1. Seleciona o primeiro botão assim que o menu abre (se ainda não houver seleção).
///  2. Quando o jogador aperta qualquer coisa no controle, garante que exista uma seleção,
///     para o menu responder na hora, sem precisar de mouse.
///
/// Não desenha nada e não interfere no mouse: se o jogador usa o mouse, o uGUI já resolve
/// o hover e o clique sozinho.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/UI/Controle No Menu")]
public sealed class ControleNoMenu : MonoBehaviour
{
    [Tooltip("Selecionar automaticamente o primeiro botão ao abrir a cena.")]
    [SerializeField] private bool selecionarAoAbrir = true;

    private static ControleNoMenu instancia;

    private void Awake()
    {
        instancia = this;
    }

    private void OnEnable()
    {
        if (selecionarAoAbrir) GarantirSelecao();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void Update()
    {
        // Só age quando o jogador realmente usa o controle.
        Gamepad controle = Gamepad.current;
        if (controle == null) return;

        if (!AlgumBotaoDoControlePressionado(controle)) return;

        GarantirSelecao();
    }

    /// <summary>
    /// Se nada está selecionado, seleciona o primeiro botão disponível. Não rouba a seleção
    /// de quem já está navegando.
    /// </summary>
    public static void GarantirSelecao()
    {
        EventSystem sistema = EventSystem.current;
        if (sistema == null) return;
        if (sistema.currentSelectedGameObject != null) return;

        Selectable primeiro = PrimeiroSelecionavel();
        if (primeiro == null) return;

        sistema.SetSelectedGameObject(primeiro.gameObject);
    }

    /// <summary>
    /// O primeiro botão que o jogador consegue usar agora. Ignora os que estão desligados ou
    /// desativados (ex.: botão de continuar que só aparece em certos painéis).
    /// </summary>
    private static Selectable PrimeiroSelecionavel()
    {
        Selectable melhor = null;
        float melhorOrdem = float.MaxValue;

        foreach (Selectable candidato in FindObjectsByType<Selectable>(FindObjectsSortMode.None))
        {
            if (candidato == null || !candidato.IsInteractable()) continue;
            if (!candidato.gameObject.activeInHierarchy) continue;

            // O componente Canvas de um painel desligado mantém os filhos "activeInHierarchy"
            // falsos, então o teste acima já cobre os painéis fechados.

            float ordem = OrdemNaTela(candidato);
            if (ordem < melhorOrdem)
            {
                melhorOrdem = ordem;
                melhor = candidato;
            }
        }

        return melhor;
    }

    /// <summary>
    /// Ordem de leitura: mais para cima primeiro e, na mesma altura, da esquerda para a direita.
    /// Faz o primeiro botão ser o do topo, que é o "Jogar".
    /// </summary>
    private static float OrdemNaTela(Selectable candidato)
    {
        RectTransform rt = candidato.transform as RectTransform;
        if (rt == null) return float.MaxValue;

        Vector3 posicao = rt.position;
        return -posicao.y * 10000f + posicao.x;
    }

    private static bool AlgumBotaoDoControlePressionado(Gamepad controle)
    {
        if (controle.buttonSouth.wasPressedThisFrame) return true;
        if (controle.buttonNorth.wasPressedThisFrame) return true;
        if (controle.buttonEast.wasPressedThisFrame) return true;
        if (controle.buttonWest.wasPressedThisFrame) return true;
        if (controle.startButton.wasPressedThisFrame) return true;
        if (controle.selectButton.wasPressedThisFrame) return true;

        if (controle.leftStick.ReadValue().magnitude > 0.5f) return true;
        if (controle.rightStick.ReadValue().magnitude > 0.5f) return true;
        if (controle.dpad.ReadValue().magnitude > 0.5f) return true;

        return false;
    }
}
