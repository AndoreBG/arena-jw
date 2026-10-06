using UnityEngine;

// AULA 2 — VERSÃO 3
// Faz a barra do inimigo ficar sempre virada para a câmera.
// Coloque este script no Canvas (World Space) que fica acima da cabeça.
public class OlharParaCamera : MonoBehaviour
{
    private Camera cameraPrincipal;

    private void Awake()
    {
        cameraPrincipal = Camera.main;
    }

    // LateUpdate roda depois de todos os Update do quadro,
    // então a câmera já está na posição final.
    private void LateUpdate()
    {
        if (cameraPrincipal == null)
            return;

        transform.forward =
            cameraPrincipal.transform.forward;
    }
}
