using UnityEngine;

// AULA 4
// Guarda o estado da partida e decide QUANDO salvar.
// Coloque num objeto vazio da cena chamado GerenciadorJogo.
//
// Precisa dos arquivos da pasta JSON: DadosJogo.cs e ArmazenamentoJogo.cs.
[RequireComponent(typeof(ArmazenamentoJogo))]
public class GerenciadorJogo : MonoBehaviour
{
    [Header("Pontuação")]
    [SerializeField] private int pontosPorInimigo = 10;

    private ArmazenamentoJogo armazenamento;
    private DadosJogo dados;

    public DadosJogo Dados => dados;

    private void Awake()
    {
        armazenamento = GetComponent<ArmazenamentoJogo>();

        // A primeira coisa que o jogo faz é buscar o save.
        dados = armazenamento.Carregar();
    }

    public void RegistrarMorteInimigo()
    {
        dados.pontos += pontosPorInimigo;

        if (dados.pontos > dados.recorde)
            dados.recorde = dados.pontos;
    }

    public void PerderVida()
    {
        dados.vidas -= 1;

        if (dados.vidas < 0)
            dados.vidas = 0;
    }

    public void ConcluirFase()
    {
        dados.fase += 1;

        // Escrever em disco é lento: salvamos ao vencer a fase, não a cada ponto.
        armazenamento.Salvar(dados);
    }

    public void ReiniciarPartida()
    {
        dados.pontos = 0;
        dados.fase = 1;
        dados.vidas = 3;

        armazenamento.Salvar(dados);
    }

    // A Unity chama quando o jogo está fechando: última chance de gravar.
    // No celular, use também OnApplicationPause.
    private void OnApplicationQuit()
    {
        armazenamento.Salvar(dados);
    }
}
