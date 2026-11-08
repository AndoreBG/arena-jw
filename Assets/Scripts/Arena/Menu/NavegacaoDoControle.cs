using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mantém a navegação do menu viva quando o jogador usa o controle.
///
/// O Unity só move a seleção do <see cref="UnityEngine.EventSystems.EventSystem"/> pelo
/// D-pad/stick quando já existe um objeto selecionado. Alguém clicar no vazio com o mouse, ou
/// trocar de painel, zera a seleção — e aí o controle "morre". É o sintoma relatado: o mouse
/// funciona, o controle não faz nada.
///
/// Este componente confere a cada quadro se o jogador mexeu no controle e, se mexeu e não há
/// seleção, elege a primeira opção do painel aberto. Depois disso o Unity processa o D-pad
/// normalmente.
///
/// Roda antes do EventSystem (<see cref="DefaultExecutionOrder"/> negativo) para a seleção já
/// estar no lugar no mesmo quadro em que o jogador aperta o botão — sem isso, o primeiro
/// toque é engolido e o jogador acha que o menu travou.
///
/// Lê o <see cref="Gamepad"/>.current a cada quadro em vez de assinar eventos: controle
/// conectado ou trocado no meio do jogo passa a valer sozinho, e não há assinatura para
/// vazar ao recarregar a cena.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[AddComponentMenu("Arena/UI/Navegacao Do Controle")]
public sealed class NavegacaoDoControle : MonoBehaviour
{
    private void Update()
    {
        Gamepad controle = Gamepad.current;
        if (controle == null) return;

        if (!AlgumUsoDoControle(controle)) return;

        // Não rouba a seleção de quem já está navegando, e resolve o caso da seleção ter
        // apontado para um botão que o painel anterior levou embora.
        UnityEngine.EventSystems.EventSystem sistema = UnityEngine.EventSystems.EventSystem.current;
        if (sistema == null) return;

        GameObject atual = sistema.currentSelectedGameObject;
        bool selecaoValida = atual != null && atual.activeInHierarchy &&
                             atual.GetComponent<UnityEngine.UI.Selectable>()?.IsInteractable() == true;

        if (!selecaoValida)
            ArenaGameInitializer.EnsureMenuNavigable();
    }

    /// <summary>Qualquer botão, D-pad ou analógico: o que importa é o jogador ter pegado no controle.</summary>
    private static bool AlgumUsoDoControle(Gamepad controle)
    {
        if (controle.buttonSouth.wasPressedThisFrame) return true;
        if (controle.buttonNorth.wasPressedThisFrame) return true;
        if (controle.buttonEast.wasPressedThisFrame) return true;
        if (controle.buttonWest.wasPressedThisFrame) return true;
        if (controle.startButton.wasPressedThisFrame) return true;
        if (controle.selectButton.wasPressedThisFrame) return true;

        if (controle.dpad.ReadValue().sqrMagnitude > 0.25f) return true;
        if (controle.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;

        return false;
    }
}
