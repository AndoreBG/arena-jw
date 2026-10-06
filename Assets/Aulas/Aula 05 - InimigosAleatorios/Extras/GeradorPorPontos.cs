using UnityEngine;

// AULA 5 — EXTRA
// A outra forma de sortear: em vez de um retângulo, uma lista de pontos
// marcados à mão na cena.
//
// Use quando a arena tiver paredes, obstáculos ou buracos — sortear no
// retângulo pode colocar o inimigo dentro de uma parede.
public class GeradorPorPontos : MonoBehaviour
{
    [Header("Inimigo")]
    [SerializeField] private GameObject inimigoPrefab;

    // Crie objetos vazios na cena, posicione onde quiser, e arraste
    // todos para esta lista no Inspector.
    [Header("Pontos marcados")]
    [SerializeField] private Transform[] pontosSpawn;

    public void Gerar()
    {
        if (inimigoPrefab == null)
            return;

        if (pontosSpawn == null || pontosSpawn.Length == 0)
            return;

        // Aqui o Random.Range de int faz sentido: Length é a quantidade
        // de pontos, e os índices vão de 0 até Length menos 1 —
        // exatamente o que este sorteio devolve.
        int indiceSorteado = Random.Range(0, pontosSpawn.Length);

        Transform pontoEscolhido = pontosSpawn[indiceSorteado];

        Instantiate(
            inimigoPrefab,
            pontoEscolhido.position,
            pontoEscolhido.rotation
        );
    }
}
