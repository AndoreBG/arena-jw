using UnityEngine;
using UnityEngine.UI;

// AULA 2 — VERSÃO 1
// A barra PERGUNTA a vida a cada quadro.
// Funciona, mas faz 60 perguntas por segundo para um valor que muda pouco.
// O sufixo no nome da classe evita erro de compilação: no Unity, não podem existir
// duas classes com o mesmo nome no projeto (todas as versões da aula ficam nele).
public class BarraVidaV1 : MonoBehaviour
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
