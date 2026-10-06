using UnityEngine;
using UnityEngine.InputSystem;

// SCRIPT PROVISÓRIO — SÓ PARA A AULA
// Serve para testar a barra sem depender de inimigo ou projétil.
// Apague depois que o dano passar a vir do Projetil.cs e do DanoPorContato.cs.
//
// ATENÇÃO: se o PuloPlayer.cs estiver ativo no mesmo objeto, a barra de espaço
// vai pular e tirar vida ao mesmo tempo. Troque spaceKey por outra tecla.
public class TesteDano : MonoBehaviour
{
    [Header("Alvo")]
    [SerializeField] private Vida vida;

    [Header("Valores")]
    [SerializeField] private float danoPorTecla = 10f;
    [SerializeField] private float curaPorTecla = 10f;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (vida == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            vida.ReceberDano(danoPorTecla);

        if (Keyboard.current.hKey.wasPressedThisFrame)
            vida.Curar(curaPorTecla);
    }
}
