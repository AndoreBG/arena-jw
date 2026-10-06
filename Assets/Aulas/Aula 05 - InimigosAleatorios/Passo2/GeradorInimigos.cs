using UnityEngine;
using UnityEngine.InputSystem;

// AULA 5 — PASSO 2
// Agora a posição é sorteada dentro da área da arena.
// Continua nascendo na tecla E.
public class GeradorInimigos : MonoBehaviour
{
    [Header("Inimigo")]
    [SerializeField] private GameObject inimigoPrefab;

    // Digite aqui o tamanho inteiro do chão, não a metade.
    // Plane de 40 por 40? Digite 40 e 40.
    [Header("Área da arena")]
    [SerializeField] private float larguraDaArena = 40f;
    [SerializeField] private float comprimentoDaArena = 40f;
    [SerializeField] private float alturaDeNascimento = 1f;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
            Gerar();
    }

    private void Gerar()
    {
        if (inimigoPrefab == null)
            return;

        Vector3 posicaoSorteada = SortearPosicao();

        Instantiate(inimigoPrefab, posicaoSorteada, Quaternion.identity);
    }

    // Este método devolve uma posição. O tipo antes do nome diz o que sai dele.
    private Vector3 SortearPosicao()
    {
        float metadeDaLargura = larguraDaArena / 2f;
        float metadeDoComprimento = comprimentoDaArena / 2f;

        // Com float (o f nos números), o limite entra no sorteio.
        float posicaoX = Random.Range(-metadeDaLargura, metadeDaLargura);
        float posicaoZ = Random.Range(-metadeDoComprimento, metadeDoComprimento);

        // X e Z são o chão. A altura é fixa: sorteada, o inimigo nasceria
        // no ar ou enterrado.
        return new Vector3(posicaoX, alturaDeNascimento, posicaoZ);

        // Se a sua arena não estiver centrada na origem, use:
        // return transform.position
        //     + new Vector3(posicaoX, alturaDeNascimento, posicaoZ);
    }
}
