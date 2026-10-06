using UnityEngine;
using UnityEngine.UI;

// AULA 2 — VERSÃO 3 (a que vai para o projeto)
// Igual à versão 2, mais a opção de esconder a barra com a vida cheia.
// Serve para o player e para o inimigo, sem mudar uma linha.
public class BarraVida : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Vida vida;
    [SerializeField] private Image preenchimento;

    [Header("Opcional")]
    [Tooltip("Objeto que será desligado quando a vida estiver cheia.")]
    [SerializeField] private GameObject raiz;

    private void OnEnable()
    {
        if (vida == null)
            return;

        vida.AoAlterarVida += AtualizarBarra;

        AtualizarBarra(vida.VidaAtual, vida.VidaMaxima);
    }

    private void OnDisable()
    {
        if (vida == null)
            return;

        vida.AoAlterarVida -= AtualizarBarra;
    }

    private void AtualizarBarra(float atual, float maxima)
    {
        if (preenchimento == null)
            return;

        preenchimento.fillAmount = atual / maxima;

        if (raiz != null)
            raiz.SetActive(atual < maxima);
    }
}
