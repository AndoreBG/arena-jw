using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// AULA 3 — PASSO 3 (versão final)
// O número agora sobrevive ao fechar o jogo.
//
// Teste: aumente, pare o Play, dê Play de novo. O valor continua lá.
public class Contador : MonoBehaviour
{
    [SerializeField] private TMP_Text texto;

    // A etiqueta é só texto: o compilador não confere.
    // Escrever "Valor" com V maiúsculo não dá erro, só devolve 0 para sempre.
    private const string Chave = "valor";

    private int valor = 0;

    private void Start()
    {
        // O segundo parâmetro é o que vem se a etiqueta ainda não existir.
        valor = PlayerPrefs.GetInt(Chave, 0);

        Mostrar();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

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

    // A Unity chama quando o jogo está fechando — inclusive ao sair do Play.
    private void OnApplicationQuit()
    {
        PlayerPrefs.SetInt(Chave, valor);

        // Sem o Save(), a gravação fica só anotada na memória.
        PlayerPrefs.Save();
    }
}
