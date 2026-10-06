using System;
using UnityEngine;

// AULA 2 — ARQUIVO DE REFERÊNCIA
// Este script já faz parte do projeto. Na aula 2 ele é apenas LIDO,
// não precisa ser alterado.
public class Vida : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float vidaMaxima = 100f;

    [Header("Morte")]
    [SerializeField] private bool destruirAoMorrer = true;
    [SerializeField] private float tempoParaDestruir = 0f;

    private float vidaAtual;
    private bool morto;

    public float VidaAtual => vidaAtual;
    public float VidaMaxima => vidaMaxima;
    public bool Morto => morto;

    public event Action<float, float> AoAlterarVida;
    public event Action AoMorrer;

    private void Awake()
    {
        vidaAtual = vidaMaxima;
    }

    public void ReceberDano(float dano)
    {
        if (morto)
            return;

        if (dano <= 0)
            return;

        vidaAtual -= dano;
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);

        AoAlterarVida?.Invoke(vidaAtual, vidaMaxima);

        if (vidaAtual <= 0)
        {
            Morrer();
        }
    }

    public void Curar(float quantidade)
    {
        if (morto)
            return;

        if (quantidade <= 0)
            return;

        vidaAtual += quantidade;
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);

        AoAlterarVida?.Invoke(vidaAtual, vidaMaxima);
    }

    public void RestaurarVida()
    {
        morto = false;
        vidaAtual = vidaMaxima;

        AoAlterarVida?.Invoke(vidaAtual, vidaMaxima);
    }

    private void Morrer()
    {
        if (morto)
            return;

        morto = true;

        AoMorrer?.Invoke();

        if (destruirAoMorrer)
        {
            Destroy(gameObject, tempoParaDestruir);
        }
    }
}
