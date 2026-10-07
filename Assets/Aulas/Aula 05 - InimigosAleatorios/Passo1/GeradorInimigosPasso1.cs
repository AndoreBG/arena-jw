using UnityEngine;
using UnityEngine.InputSystem;

// AULA 5 — PASSO 1
// Cria um inimigo no centro da arena quando você aperta E.
// Todos nascem empilhados no mesmo ponto — isso é de propósito.
// O sufixo no nome da classe evita erro de compilação: no Unity, não podem existir
// duas classes com o mesmo nome no projeto (todas as versões da aula ficam nele).
public class GeradorInimigosPasso1 : MonoBehaviour
{
    // Arraste aqui, no Inspector, o prefab do inimigo.
    [Header("Inimigo")]
    [SerializeField] private GameObject inimigoPrefab;

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

        // Vector3.zero é o centro do mundo: new Vector3(0, 0, 0).
        // Quaternion.identity quer dizer "sem rotação".
        Instantiate(inimigoPrefab, Vector3.zero, Quaternion.identity);
    }
}
