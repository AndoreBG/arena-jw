using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Diagnóstico do controle: mostra na tela o que o Unity está recebendo de verdade.
///
/// Existe porque "o controle não funciona" tem várias causas possíveis (modo DInput,
/// aparelho não reconhecido, botão mapeado errado) e adivinhar de longe queima rodada.
/// Com o painel aberto, dá para ver qual aparelho foi detectado, se ele é tratado como
/// Gamepad, e qual botão acende quando o jogador aperta cada tecla.
///
/// Liga e desliga com F1 (só no Editor e em builds de desenvolvimento), ou pelo campo
/// "Mostrar Ao Iniciar" no Inspector. Não interfere no jogo: os botões continuam
/// funcionando normalmente enquanto o painel está aberto.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Debug/Diagnostico De Controle")]
public sealed class DiagnosticoDeControle : MonoBehaviour
{
    private const UnityEngine.InputSystem.Key TeclaDeAlternancia =
        UnityEngine.InputSystem.Key.F1;
    private const float IntervaloDeVerificacao = 1f;

    [Tooltip("Abre o painel assim que a partida começa.")]
    [SerializeField] private bool mostrarAoIniciar;

    private bool mostrar;
    private readonly StringBuilder texto = new StringBuilder(1024);
    private GUIStyle estilo;
    private float proximaVerificacao;
    private string aparelhos = "procurando...";

    private void Start()
    {
        mostrar = mostrarAoIniciar;
    }

    private void Update()
    {
        AtualizarAtalho();
        AtualizarListaDeAparelhos();
    }

    /// <summary>F1 abre e fecha. Usa o Input System para funcionar mesmo com o cursor travado.</summary>
    private void AtualizarAtalho()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;
        if (!teclado[TeclaDeAlternancia].wasPressedThisFrame) return;

        mostrar = !mostrar;
    }

    /// <summary>
    /// Reenumera os aparelhos de tempos em tempos (conectar/desconectar controle no meio da
    /// partida é comum, e InputSystem.devices é alocado a cada leitura).
    /// </summary>
    private void AtualizarListaDeAparelhos()
    {
        if (Time.unscaledTime < proximaVerificacao) return;
        proximaVerificacao = Time.unscaledTime + IntervaloDeVerificacao;

        var lista = InputSystem.devices;
        texto.Clear();

        for (int i = 0; i < lista.Count; i++)
        {
            InputDevice aparelho = lista[i];
            if (aparelho == null) continue;

            // Só interessa o que pode ser controle ou teclado.
            bool relevante = aparelho is Gamepad || aparelho is Joystick ||
                             aparelho is Keyboard || aparelho is Mouse;
            if (!relevante) continue;

            if (texto.Length > 0) texto.Append('\n');
            texto.Append("  ").Append(aparelho.displayName);

            // O tipo de verdade que o Unity resolveu é o que importa: um controle em modo
            // DInput aparece como Joystick e NÃO responde a <Gamepad>.
            if (aparelho is Gamepad) texto.Append("  [Gamepad ✔]");
            else if (aparelho is Joystick) texto.Append("  [Joystick ✘ — modo errado?]");
            else if (aparelho is Keyboard) texto.Append("  [teclado]");
            else if (aparelho is Mouse) texto.Append("  [mouse]");
        }

        aparelhos = texto.Length > 0 ? texto.ToString() : "  (nenhum aparelho de entrada)";
    }

    private void OnGUI()
    {
        if (!mostrar) return;

        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = false,
                wordWrap = false,
            };
            estilo.normal.textColor = Color.white;
        }

        Gamepad controle = Gamepad.current;

        var painel = new StringBuilder(2048);
        painel.AppendLine("=== DIAGNÓSTICO DE CONTROLE (F1 fecha) ===");
        painel.AppendLine();
        painel.AppendLine("Aparelhos que o Unity vê:");
        painel.AppendLine(aparelhos);
        painel.AppendLine();

        if (controle == null)
        {
            painel.AppendLine("Gamepad.current = NULO");
            painel.AppendLine();
            painel.AppendLine("O jogo não está vendo um Gamepad.");
            painel.AppendLine("O controle provavelmente está em modo DInput ou Switch.");
            painel.AppendLine("Desligue o controle e ligue segurando X (XInput) e conecte");
            painel.AppendLine("o receptor 2.4 GHz direto no PC, sem hub.");
            painel.AppendLine("Se aparecer [Joystick] na lista acima, é isso.");
        }
        else
        {
            painel.AppendLine("Gamepad.current: " + controle.displayName);
            painel.AppendLine();
            painel.AppendLine("  Botões (aceso = pressionado):");

            painel.AppendLine("    Sul   (A/✕) : " + Marca(controle.buttonSouth));
            painel.AppendLine("    Leste (B/○) : " + Marca(controle.buttonEast));
            painel.AppendLine("    Oeste (X/□) : " + Marca(controle.buttonWest));
            painel.AppendLine("    Norte (Y/△) : " + Marca(controle.buttonNorth));
            painel.AppendLine("    LB / L1     : " + Marca(controle.leftShoulder));
            painel.AppendLine("    RB / R1     : " + Marca(controle.rightShoulder));
            painel.AppendLine("    Start       : " + Marca(controle.startButton));
            painel.AppendLine("    Select      : " + Marca(controle.selectButton));

            painel.AppendLine();
            painel.AppendLine("  Analógicos:");
            painel.AppendLine("    Esquerdo : " + Vetor(controle.leftStick.ReadValue()));
            painel.AppendLine("    Direito  : " + Vetor(controle.rightStick.ReadValue()));
            painel.AppendLine("    D-pad    : " + Vetor(controle.dpad.ReadValue()));

            painel.AppendLine();
            painel.AppendLine("  Gatilhos:");
            painel.AppendLine("    LT / L2 : " + controle.leftTrigger.ReadValue().ToString("0.00"));
            painel.AppendLine("    RT / R2 : " + controle.rightTrigger.ReadValue().ToString("0.00"));
        }

        painel.AppendLine();
        painel.AppendLine("Cursores: " + Cursor.lockState);
        painel.AppendLine("Selecionado no EventSystem: " + NomeDoSelecionado());

        // Cor de fundo para o texto ficar legível sobre a cena.
        Rect area = new Rect(10f, 10f, 560f, 460f);
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f),
                  painel.ToString(), estilo);
    }

    private static string Marca(UnityEngine.InputSystem.Controls.ButtonControl botao)
    {
        if (botao == null) return "?";
        return botao.isPressed ? "████  PRESSIONADO" : "·";
    }

    private static string Vetor(Vector2 v)
    {
        return v.x.ToString("0.00") + ", " + v.y.ToString("0.00");
    }

    private static string NomeDoSelecionado()
    {
        var sistema = UnityEngine.EventSystems.EventSystem.current;
        if (sistema == null) return "(sem EventSystem)";

        GameObject alvo = sistema.currentSelectedGameObject;
        return alvo != null ? alvo.name : "(nada)";
    }
}
