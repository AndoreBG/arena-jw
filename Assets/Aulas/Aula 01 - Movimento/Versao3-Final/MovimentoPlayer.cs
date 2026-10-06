using UnityEngine;
using UnityEngine.InputSystem;

// AULA 1 — VERSÃO 3 (a que vai para o projeto)
// Move pelo Rigidbody, dentro do FixedUpdate: respeita colisão e gravidade.
// Atenção: rb.linearVelocity existe a partir da Unity 6.
// Em versões anteriores, troque por rb.velocity.
[RequireComponent(typeof(Rigidbody))]
public class MovimentoPlayer : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float velocidade = 6f;

    private Rigidbody rb;
    private Vector2 entradaMovimento;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        LerEntrada();
    }

    private void FixedUpdate()
    {
        Mover();
    }

    private void LerEntrada()
    {
        entradaMovimento = Vector2.zero;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.wKey.isPressed)
            entradaMovimento.y += 1;

        if (Keyboard.current.sKey.isPressed)
            entradaMovimento.y -= 1;

        if (Keyboard.current.dKey.isPressed)
            entradaMovimento.x += 1;

        if (Keyboard.current.aKey.isPressed)
            entradaMovimento.x -= 1;

        entradaMovimento = Vector2.ClampMagnitude(
            entradaMovimento,
            1f
        );
    }

    private void Mover()
    {
        Vector3 movimento = new Vector3(
            entradaMovimento.x,
            0f,
            entradaMovimento.y
        );

        Vector3 velocidadeAtual = rb.linearVelocity;

        rb.linearVelocity = new Vector3(
            movimento.x * velocidade,
            velocidadeAtual.y,
            movimento.z * velocidade
        );
    }
}
