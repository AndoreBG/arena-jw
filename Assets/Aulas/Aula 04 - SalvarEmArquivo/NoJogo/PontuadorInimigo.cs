using UnityEngine;

// AULA 4
// Este é o desafio de casa da aula 2: o evento AoMorrer do Vida.cs
// já existia e ninguém estava inscrito nele.
//
// Coloque no prefab do inimigo, junto com o Vida.cs.
[RequireComponent(typeof(Vida))]
public class PontuadorInimigo : MonoBehaviour
{
    private Vida vida;
    private GerenciadorJogo gerenciador;

    private void Awake()
    {
        vida = GetComponent<Vida>();

        // O inimigo nasce durante a partida, então não dá para arrastar
        // a referência do gerenciador no Inspector do prefab.
        // Em versões anteriores à Unity 6: FindObjectOfType<GerenciadorJogo>()
        gerenciador = FindFirstObjectByType<GerenciadorJogo>();
    }

    private void OnEnable()
    {
        vida.AoMorrer += Pontuar;
    }

    private void OnDisable()
    {
        vida.AoMorrer -= Pontuar;
    }

    // AoMorrer é um Action sem < >: não envia valor nenhum,
    // então este método não tem parâmetro.
    private void Pontuar()
    {
        if (gerenciador != null)
            gerenciador.RegistrarMorteInimigo();
    }
}
