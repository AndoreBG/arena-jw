using UnityEngine;
using UnityEngine.UI;

// AULA 2 — VERSÃO 2
// A vida AVISA quando muda. A barra só trabalha quando precisa.
// Repare: não existe mais nenhum Update nesta classe.
public class BarraVida : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Vida vida;
    [SerializeField] private Image preenchimento;

    private void OnEnable()
    {
        if (vida == null)
            return;

        vida.AoAlterarVida += AtualizarBarra;

        // Primeira atualização na mão: o evento só avisa quando MUDA,
        // e no começo da partida ainda não mudou nada.
        AtualizarBarra(vida.VidaAtual, vida.VidaMaxima);
    }

    private void OnDisable()
    {
        if (vida == null)
            return;

        vida.AoAlterarVida -= AtualizarBarra;
    }

    // A assinatura precisa bater com Action<float, float>:
    // dois parâmetros do tipo float, nessa ordem.
    private void AtualizarBarra(float atual, float maxima)
    {
        if (preenchimento == null)
            return;

        preenchimento.fillAmount = atual / maxima;
    }
}
