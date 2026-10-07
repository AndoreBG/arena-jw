using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// AULA 3 — PASSO 2
// Agora o número muda com as setas. Ainda não grava nada.
// O sufixo no nome da classe evita erro de compilação: no Unity, não podem existir
// duas classes com o mesmo nome no projeto (todas as versões da aula ficam nele).
public class ContadorPasso2 : MonoBehaviour
{
    [SerializeField] private TMP_Text texto;

    private int valor = 0;

    private void Start()
    {
        Mostrar();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // wasPressedThisFrame: um toque, uma vez.
        // Com isPressed, segurar a tecla somaria 60 por segundo.
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            valor += 1;
            Mostrar();
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            valor -= 1;
            Mostrar();
        }
    }

    private void Mostrar()
    {
        texto.text = "Valor: " + valor;
    }
}
