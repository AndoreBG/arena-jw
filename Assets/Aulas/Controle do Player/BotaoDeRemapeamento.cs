using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Vai em cada botão da tela de configuração.
// Ao clicar, espera o jogador apertar uma tecla ou um botão do controle
// e preenche a opção sozinho.
[RequireComponent(typeof(Button))]
public class BotaoDeRemapeamento : MonoBehaviour
{
    [Header("Qual opção este botão configura")]
    public string nomeDaAcao = "Atirar";          // Mover, Atirar ou Pausar
    public string parteDoMovimento = "";          // Up, Down, Left, Right (só no Mover do teclado)
    public bool configuraOControle = false;       // desmarcado = teclado, marcado = controle

    [Header("Texto que aparece no botão")]
    public TMP_Text textoDoBotao;

    private InputActionRebindingExtensions.RebindingOperation operacaoDeRemapeamento;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(IniciarRemapeamento);
    }

    void OnEnable()
    {
        AtualizarTexto();
    }

    void OnDisable()
    {
        // Se a tela fechar no meio da espera, cancela para não travar a ação
        if (operacaoDeRemapeamento != null)
        {
            operacaoDeRemapeamento.Cancel();
        }
    }

    public void AtualizarTexto()
    {
        InputAction acao = ControlesDoJogo.Instancia.BuscarAcaoPeloNome(nomeDaAcao);
        int indiceDoBotao = EncontrarIndiceDoBotao(acao);
        if (indiceDoBotao < 0)
        {
            textoDoBotao.text = "?";
            return;
        }

        string nomeDoBotao = acao.GetBindingDisplayString(indiceDoBotao);
        textoDoBotao.text = string.IsNullOrEmpty(nomeDoBotao) ? "(vazio)" : nomeDoBotao;
    }

    private void IniciarRemapeamento()
    {
        if (operacaoDeRemapeamento != null) return;  // já está esperando

        InputAction acao = ControlesDoJogo.Instancia.BuscarAcaoPeloNome(nomeDaAcao);
        int indiceDoBotao = EncontrarIndiceDoBotao(acao);
        if (indiceDoBotao < 0) return;

        // A ação precisa estar desligada enquanto troca o botão
        acao.Disable();
        textoDoBotao.text = configuraOControle ? "Aperte no controle..." : "Aperte uma tecla...";

        operacaoDeRemapeamento = acao.PerformInteractiveRebinding(indiceDoBotao)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithMagnitudeHavingToBeGreaterThan(0.5f)   // ignora stick "tremendo" parado
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operacao => TerminarRemapeamento(acao, foiConcluido: true))
            .OnCancel(operacao => TerminarRemapeamento(acao, foiConcluido: false));

        // Só aceita aparelhos do tipo certo para esta opção
        if (configuraOControle)
        {
            operacaoDeRemapeamento.WithControlsHavingToMatchPath("<Joystick>");
            operacaoDeRemapeamento.WithControlsHavingToMatchPath("<Gamepad>");
        }
        else
        {
            operacaoDeRemapeamento.WithControlsHavingToMatchPath("<Keyboard>");
        }

        operacaoDeRemapeamento.Start();
    }

    private void TerminarRemapeamento(InputAction acao, bool foiConcluido)
    {
        operacaoDeRemapeamento.Dispose();
        operacaoDeRemapeamento = null;
        acao.Enable();

        if (foiConcluido)
        {
            ControlesDoJogo.Instancia.SalvarConfiguracao();
        }
        AtualizarTexto();
    }

    // Procura, dentro da ação, qual botão este campo representa
    private int EncontrarIndiceDoBotao(InputAction acao)
    {
        string grupoProcurado = configuraOControle ? ControlesDoJogo.grupoControle : ControlesDoJogo.grupoTeclado;
        bool procuraParteDoMovimento = !string.IsNullOrEmpty(parteDoMovimento);

        for (int indice = 0; indice < acao.bindings.Count; indice++)
        {
            InputBinding botao = acao.bindings[indice];
            bool mesmoGrupo = botao.groups != null && botao.groups.Contains(grupoProcurado);
            bool mesmaParte = procuraParteDoMovimento
                ? botao.isPartOfComposite && string.Equals(botao.name, parteDoMovimento, System.StringComparison.OrdinalIgnoreCase)
                : !botao.isPartOfComposite;

            if (mesmoGrupo && mesmaParte) return indice;
        }
        return -1;
    }
}
