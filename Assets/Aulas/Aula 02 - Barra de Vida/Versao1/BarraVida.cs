using UnityEngine;
using UnityEngine.UI;

// AULA 2 — VERSÃO 1
// A barra PERGUNTA a vida a cada quadro.
// Funciona, mas faz 60 perguntas por segundo para um valor que muda pouco.
public class BarraVida : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Vida vida;
    [SerializeField] private Image preenchimento;

    private void Update()
    {
        if (vida == null || preenchimento == null)
            return;

        preenchimento.fillAmount =
            vida.VidaAtual / vida.VidaMaxima;
    }
}
