using System.IO;
using UnityEngine;

// AULA 4
// Esta classe só sabe escrever e ler o arquivo. Ela não guarda os dados
// nem decide quando salvar: recebe tudo pronto de quem a usa.
public class ArmazenamentoJogo : MonoBehaviour
{
    [Header("Arquivo")]
    [SerializeField] private string nomeArquivo = "save.json";

    // Propriedade só de leitura, calculada na hora.
    // Path.Combine junta pasta e nome com a barra certa de cada sistema.
    private string Caminho =>
        Path.Combine(
            Application.persistentDataPath,
            nomeArquivo
        );

    private void Awake()
    {
        // Rode uma vez e copie o caminho do Console para abrir a pasta.
        Debug.Log("Save em: " + Caminho);
    }

    public void Salvar(DadosJogo dados)
    {
        // O true deixa o texto formatado e legível.
        string json = JsonUtility.ToJson(dados, true);

        // ATENÇÃO: WriteAllText substitui o arquivo inteiro, sem perguntar.
        File.WriteAllText(Caminho, json);
    }

    public DadosJogo Carregar()
    {
        // Primeira partida: ainda não existe arquivo.
        if (!File.Exists(Caminho))
            return new DadosJogo();

        try
        {
            string json = File.ReadAllText(Caminho);
            DadosJogo dados = JsonUtility.FromJson<DadosJogo>(json);

            // Arquivo vazio faz o FromJson devolver null.
            // O ?? troca esse null por dados novos.
            return dados ?? new DadosJogo();
        }
        catch (IOException erro)
        {
            // Plano B: arquivo corrompido, sem permissão, pendrive removido.
            Debug.LogWarning("Não foi possível ler o save: " + erro.Message);
            return new DadosJogo();
        }
    }

    public void Apagar()
    {
        if (File.Exists(Caminho))
            File.Delete(Caminho);
    }
}
