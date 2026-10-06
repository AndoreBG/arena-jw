using UnityEngine;
using UnityEngine.InputSystem;

// AULA 1 — VERSÃO 2
// Agora obedece às teclas W, A, S e D.
// Ainda usa transform.Translate, então ATRAVESSA PAREDES. Isso é proposital:
// é o problema que a versão 3 resolve.
public class MovimentoPlayer : MonoBehaviour
{
    public float velocidade = 6f;

    void Update()
    {
        float passo = velocidade * Time.deltaTime;

        if (Keyboard.current.wKey.isPressed)
            transform.Translate(Vector3.forward * passo);

        if (Keyboard.current.sKey.isPressed)
            transform.Translate(Vector3.back * passo);

        if (Keyboard.current.dKey.isPressed)
            transform.Translate(Vector3.right * passo);

        if (Keyboard.current.aKey.isPressed)
            transform.Translate(Vector3.left * passo);
    }
}
