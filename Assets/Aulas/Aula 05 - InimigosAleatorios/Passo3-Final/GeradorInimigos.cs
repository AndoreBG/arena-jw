using UnityEngine;

// AULA 5 — PASSO 3 (versão final)
// Os inimigos nascem sozinhos, de tempos em tempos, em posições sorteadas,
// até o limite da fase.
//
// Coloque num objeto vazio da cena chamado GeradorInimigos.
public class GeradorInimigos : MonoBehaviour
{
    [Header("Inimigo")]
    [SerializeField] private GameObject inimigoPrefab;

    // Tamanho inteiro do chão, não a metade.
    [Header("Área da arena")]
    [SerializeField] private float larguraDaArena = 40f;
    [SerializeField] private float comprimentoDaArena = 40f;
    [SerializeField] private float alturaDeNascimento = 1f;

    [Header("Ritmo")]
    [SerializeField] private float intervaloEntreInimigos = 3f;
    [SerializeField] private int maximoDeInimigos = 10;

    [Header("Distância do player (opcional)")]
    [SerializeField] private Transform player;
    [SerializeField] private float distanciaMinimaDoPlayer = 6f;

    private float tempoAteOProximo;
    private int inimigosCriados;

    private void Start()
    {
        tempoAteOProximo = intervaloEntreInimigos;
    }

    private void Update()
    {
        if (inimigosCriados >= maximoDeInimigos)
            return;

        // Time.deltaTime é o tempo do último quadro: descontando a cada
        // quadro, a conta vira segundos de verdade.
        tempoAteOProximo -= Time.deltaTime;

        if (tempoAteOProximo <= 0f)
        {
            Gerar();

            // Recarregar é essencial: sem isto, a condição continuaria
            // verdadeira e nasceriam 60 inimigos por segundo.
            tempoAteOProximo = intervaloEntreInimigos;
        }
    }

    private void Gerar()
    {
        if (inimigoPrefab == null)
            return;

        Vector3 posicaoSorteada = SortearPosicao();

        // Não deixa nascer em cima do player. Se o sorteio caiu perto,
        // desiste desta vez — na próxima ele sorteia de novo.
        if (player != null &&
            Vector3.Distance(posicaoSorteada, player.position)
                < distanciaMinimaDoPlayer)
        {
            return;
        }

        Instantiate(inimigoPrefab, posicaoSorteada, Quaternion.identity);

        inimigosCriados += 1;
    }

    private Vector3 SortearPosicao()
    {
        float metadeDaLargura = larguraDaArena / 2f;
        float metadeDoComprimento = comprimentoDaArena / 2f;

        float posicaoX = Random.Range(-metadeDaLargura, metadeDaLargura);
        float posicaoZ = Random.Range(-metadeDoComprimento, metadeDoComprimento);

        return new Vector3(posicaoX, alturaDeNascimento, posicaoZ);
    }

    // Chamado por outro script quando a fase começa.
    // O gerador não decide quando a fase muda: ele só obedece.
    public void ComecarFase(int numeroDaFase)
    {
        maximoDeInimigos = 5 + (numeroDaFase * 2);

        inimigosCriados = 0;
        tempoAteOProximo = intervaloEntreInimigos;
    }
}
