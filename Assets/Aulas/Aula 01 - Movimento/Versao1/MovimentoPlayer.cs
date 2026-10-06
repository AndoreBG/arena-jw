using UnityEngine;

// AULA 1 — VERSÃO 1
// O cubo anda sozinho para a frente.
// Serve para provar que o Update() roda dezenas de vezes por segundo.
public class MovimentoPlayer : MonoBehaviour
{
    public float velocidade = 6f;

    void Update()
    {
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);
    }
}
