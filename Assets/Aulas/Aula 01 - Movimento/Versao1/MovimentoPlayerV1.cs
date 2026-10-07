using UnityEngine;

// AULA 1 — VERSÃO 1
// O cubo anda sozinho para a frente.
// Serve para provar que o Update() roda dezenas de vezes por segundo.
// O sufixo no nome da classe evita erro de compilação: no Unity, não podem existir
// duas classes com o mesmo nome no projeto (todas as versões da aula ficam nele).
public class MovimentoPlayerV1 : MonoBehaviour
{
    public float velocidade = 6f;

    void Update()
    {
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);
    }
}
