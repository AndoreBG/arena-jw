using UnityEngine;
using UnityEngine.InputSystem;

// AULA 3 — EXTRA
// Apaga o que foi gravado. Serve para repetir o teste do zero.
// Coloque no mesmo objeto do Contador e aperte a tecla R.
public class ZerarValor : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            // Apaga só a etiqueta "valor".
            PlayerPrefs.DeleteKey("valor");

            // PlayerPrefs.DeleteAll(); apagaria tudo que este jogo guardou.
            PlayerPrefs.Save();

            Debug.Log("Valor apagado. Pare o Play e rode de novo.");
        }
    }
}
