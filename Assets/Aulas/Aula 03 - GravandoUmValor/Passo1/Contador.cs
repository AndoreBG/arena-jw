using UnityEngine;
using TMPro;

// AULA 3 — PASSO 1
// Só mostra o número na tela. Ainda não muda e ainda não grava.
public class Contador : MonoBehaviour
{
    // Arraste aqui, no Inspector, o objeto de texto da interface.
    [SerializeField] private TMP_Text texto;

    private int valor = 0;

    private void Start()
    {
        Mostrar();
    }

    // Este método não calcula nada: só escreve o valor atual no texto.
    private void Mostrar()
    {
        texto.text = "Valor: " + valor;
    }
}
