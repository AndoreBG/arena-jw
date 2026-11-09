using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[AddComponentMenu("Arena/UI/Navegacao Do Controle")]
public sealed class NavegacaoDoControle : MonoBehaviour
{
    /// <summary>
    /// True quando a entrada mais recente veio de um gamepad conectado.
    /// Static para sobreviver à troca de cena (menu_principal -> playground).
    /// </summary>
    public static bool UsandoControle { get; private set; }

    private void Update()
    {
        Gamepad controle = Gamepad.current;

        if (controle == null)
        {
            UsandoControle = false;
            return;
        }

        if (AlgumUsoDoControle(controle))
        {
            UsandoControle = true;
        }
        else if (AlgumUsoDeTecladoOuMouse())
        {
            UsandoControle = false;
            return;
        }

        if (!UsandoControle) return;

        EventSystem sistema = EventSystem.current;
        if (sistema == null) return;

        GameObject atual = sistema.currentSelectedGameObject;
        bool selecaoValida = atual != null && atual.activeInHierarchy &&
                             atual.GetComponent<Selectable>()?.IsInteractable() == true;

        if (!selecaoValida)
            ArenaGameInitializer.EnsureMenuNavigable();
    }

    private static bool AlgumUsoDoControle(Gamepad controle)
    {
        if (controle.buttonSouth.wasPressedThisFrame) return true;
        if (controle.buttonNorth.wasPressedThisFrame) return true;
        if (controle.buttonEast.wasPressedThisFrame) return true;
        if (controle.buttonWest.wasPressedThisFrame) return true;
        if (controle.leftShoulder.wasPressedThisFrame) return true;
        if (controle.rightShoulder.wasPressedThisFrame) return true;
        if (controle.leftTrigger.ReadValue() > 0.5f) return true;
        if (controle.rightTrigger.ReadValue() > 0.5f) return true;
        if (controle.startButton.wasPressedThisFrame) return true;
        if (controle.selectButton.wasPressedThisFrame) return true;

        if (controle.dpad.ReadValue().sqrMagnitude > 0.25f) return true;
        if (controle.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;
        if (controle.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;

        return false;
    }

    private static bool AlgumUsoDeTecladoOuMouse()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado != null && teclado.anyKey.wasPressedThisFrame)
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame ||
                mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame)
                return true;

            if (mouse.delta.ReadValue().sqrMagnitude > 16f)
                return true;
        }

        return false;
    }
}