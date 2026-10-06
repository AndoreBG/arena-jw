using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Vai no painel de configuração.
// Mostra quais teclados e controles estão conectados e atualiza sozinho
// quando alguém pluga ou desconecta um controle.
public class TelaConfiguracaoControles : MonoBehaviour
{
    public TMP_Text textoDosDispositivos;

    void OnEnable()
    {
        InputSystem.onDeviceChange += QuandoUmDispositivoMudar;
        AtualizarListaDeDispositivos();
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= QuandoUmDispositivoMudar;
    }

    private void QuandoUmDispositivoMudar(InputDevice dispositivo, InputDeviceChange mudanca)
    {
        AtualizarListaDeDispositivos();
    }

    private void AtualizarListaDeDispositivos()
    {
        string listaDeDispositivos = "Dispositivos conectados:\n";
        bool achouAlgumControle = false;

        foreach (InputDevice dispositivo in InputSystem.devices)
        {
            if (dispositivo is Keyboard)
            {
                listaDeDispositivos += "- Teclado\n";
            }
            else if (dispositivo is Gamepad)
            {
                listaDeDispositivos += "- Controle reconhecido: " + dispositivo.displayName + "\n";
                achouAlgumControle = true;
            }
            else if (dispositivo is Joystick)
            {
                listaDeDispositivos += "- Controle genérico: " + dispositivo.displayName + "\n";
                achouAlgumControle = true;
            }
        }

        if (!achouAlgumControle)
        {
            listaDeDispositivos += "- Nenhum controle encontrado. Plugue o controle USB.\n";
        }

        textoDosDispositivos.text = listaDeDispositivos;
    }

    // Ligar no botão "Restaurar padrão"
    public void RestaurarBotoesPadrao()
    {
        ControlesDoJogo.Instancia.RestaurarPadrao();

        foreach (BotaoDeRemapeamento botao in GetComponentsInChildren<BotaoDeRemapeamento>())
        {
            botao.AtualizarTexto();
        }
    }
}
