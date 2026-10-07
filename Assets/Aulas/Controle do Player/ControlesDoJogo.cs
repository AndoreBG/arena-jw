using UnityEngine;
using UnityEngine.InputSystem;

// Guarda as ações do jogo em um lugar só.
// O Player usa para jogar e o Menu usa para trocar os botões.
// Não precisa colocar na cena: ele se cria sozinho na primeira vez que alguém usar.
public class ControlesDoJogo : MonoBehaviour
{
    // Nome usado para guardar as configurações no PlayerPrefs (lembra da aula 3?)
    private const string chaveDosControlesSalvos = "controles_personalizados";

    // Grupos: servem para separar os botões do teclado dos botões do controle
    public const string grupoTeclado = "Teclado";
    public const string grupoControle = "Controle";

    private static ControlesDoJogo instanciaAtual;

    public static ControlesDoJogo Instancia
    {
        get
        {
            if (instanciaAtual == null)
            {
                GameObject objetoDosControles = new GameObject("ControlesDoJogo");
                instanciaAtual = objetoDosControles.AddComponent<ControlesDoJogo>();
                // Continua existindo quando trocar do Menu para a Arena
                DontDestroyOnLoad(objetoDosControles);
            }
            return instanciaAtual;
        }
    }

    // "private set": os outros scripts podem ler, mas só este script cria as ações
    public InputActionMap MapaDoJogador { get; private set; }
    public InputAction AcaoMover { get; private set; }
    public InputAction AcaoAtirar { get; private set; }
    public InputAction AcaoPausar { get; private set; }

    void Awake()
    {
        CriarAcoesComBotoesPadrao();
        CarregarConfiguracaoSalva();
        MapaDoJogador.Enable();
    }

    private void CriarAcoesComBotoesPadrao()
    {
        MapaDoJogador = new InputActionMap("Jogador");

        // Mover: WASD no teclado e stick no controle
        // Input System 1.19: o parâmetro da extensão AddAction saiu como
        // expectedControlType e voltou com o nome expectedControlLayout.
        AcaoMover = MapaDoJogador.AddAction("Mover", InputActionType.Value, expectedControlLayout: "Vector2");
        AcaoMover.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w", groups: grupoTeclado)
            .With("Down", "<Keyboard>/s", groups: grupoTeclado)
            .With("Left", "<Keyboard>/a", groups: grupoTeclado)
            .With("Right", "<Keyboard>/d", groups: grupoTeclado);
        AcaoMover.AddBinding("<Joystick>/stick", processors: "stickDeadzone", groups: grupoControle);

        // Atirar: Espaço no teclado e gatilho no controle
        AcaoAtirar = MapaDoJogador.AddAction("Atirar", InputActionType.Button);
        AcaoAtirar.AddBinding("<Keyboard>/space", groups: grupoTeclado);
        AcaoAtirar.AddBinding("<Joystick>/trigger", groups: grupoControle);

        // Pausar: Esc no teclado; no controle começa vazio para o jogador escolher
        AcaoPausar = MapaDoJogador.AddAction("Pausar", InputActionType.Button);
        AcaoPausar.AddBinding("<Keyboard>/escape", groups: grupoTeclado);
        AcaoPausar.AddBinding("", groups: grupoControle);
    }

    private void CarregarConfiguracaoSalva()
    {
        if (PlayerPrefs.HasKey(chaveDosControlesSalvos))
        {
            string configuracaoSalva = PlayerPrefs.GetString(chaveDosControlesSalvos);
            MapaDoJogador.LoadBindingOverridesFromJson(configuracaoSalva);
        }
    }

    public void SalvarConfiguracao()
    {
        // Guarda só o que o jogador mudou, em formato JSON (lembra da aula 4?)
        string configuracaoAtual = MapaDoJogador.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(chaveDosControlesSalvos, configuracaoAtual);
        PlayerPrefs.Save();
    }

    public void RestaurarPadrao()
    {
        MapaDoJogador.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(chaveDosControlesSalvos);
    }

    public InputAction BuscarAcaoPeloNome(string nomeDaAcao)
    {
        return MapaDoJogador.FindAction(nomeDaAcao);
    }
}
