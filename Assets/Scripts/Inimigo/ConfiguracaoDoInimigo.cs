using UnityEngine;

/// <summary>Estados possíveis do inimigo. Ele está sempre em exatamente um deles.</summary>
public enum EstadoDoInimigo
{
    Andando,
    Perseguindo,
    Atacando
}

/// <summary>
/// Números do inimigo para uma dificuldade. Cada asset é criado e editado no Inspector.
/// Base: a lógica de estados da Aula 05 (InimigoMaquinaDeEstados).
///
/// Os três pesos são o que decide o estado: quando mais de um estado é possível no mesmo
/// frame, vence o de peso maior. Com os pesos iguais, a ordem de prioridade é
/// Atacando > Perseguindo > Andando.
/// </summary>
[CreateAssetMenu(menuName = "Arena/Configuracao do Inimigo", fileName = "Inimigo")]
public sealed class ConfiguracaoDoInimigo : ScriptableObject
{
    [Header("Pesos dos estados")]
    [Tooltip("Peso de 'andando'. Usado quando o inimigo NÃO enxerga o jogador.")]
    [SerializeField, Min(0)] private int pesoAndando = 40;

    [Tooltip("Peso de 'perseguindo'. Usado quando enxerga o jogador mas está fora do alcance.")]
    [SerializeField, Min(0)] private int pesoPerseguindo = 60;

    [Tooltip("Peso de 'atacando'. Usado quando o jogador está dentro do alcance de ataque.")]
    [SerializeField, Min(0)] private int pesoAtacando = 100;

    [Header("Visão")]
    [SerializeField, Min(0f)] private float distanciaDeVisao = 30f;
    [SerializeField, Range(0f, 360f)] private float anguloDeVisao = 160f;

    [Header("Andando")]
    [SerializeField, Min(1f)] private float raioDePatrulha = 12f;
    [SerializeField, Min(0.1f)] private float velocidadeAndando = 8f;

    [Header("Perseguindo")]
    [SerializeField, Min(0.1f)] private float velocidadePerseguindo = 12f;

    [Header("Atacando")]
    [Tooltip("Distância horizontal em que o inimigo para e começa a atirar.")]
    [SerializeField, Min(1f)] private float alcanceDeAtaque = 30f;

    [Tooltip("Segundos entre um tiro e outro.")]
    [SerializeField, Min(0.05f)] private float intervaloEntreTiros = 1f;

    [Header("Projétil")]
    [Tooltip("Velocidade do projétil, em metros por segundo. Igual à do jogador por padrão.")]
    [SerializeField, Min(0.1f)] private float velocidadeDoProjetil = 100f;

    [Tooltip("Tempo de vida do projétil, em segundos. Igual à do jogador por padrão.")]
    [SerializeField, Min(0.1f)] private float tempoDeVidaDoProjetil = 5f;

    [Tooltip("Dano de cada tiro.")]
    [SerializeField, Min(0f)] private float danoDoProjetil = 50f;

    public int PesoAndando => pesoAndando;
    public int PesoPerseguindo => pesoPerseguindo;
    public int PesoAtacando => pesoAtacando;

    public float DistanciaDeVisao => distanciaDeVisao;
    public float AnguloDeVisao => anguloDeVisao;

    public float RaioDePatrulha => raioDePatrulha;
    public float VelocidadeAndando => velocidadeAndando;
    public float VelocidadePerseguindo => velocidadePerseguindo;

    public float AlcanceDeAtaque => alcanceDeAtaque;
    public float IntervaloEntreTiros => intervaloEntreTiros;

    public float VelocidadeDoProjetil => velocidadeDoProjetil;
    public float TempoDeVidaDoProjetil => tempoDeVidaDoProjetil;
    public float DanoDoProjetil => danoDoProjetil;
}
