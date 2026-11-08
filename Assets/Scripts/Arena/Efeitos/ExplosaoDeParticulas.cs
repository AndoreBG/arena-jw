using UnityEngine;

/// <summary>
/// Explosão de partículas descartável, montada por código.
///
/// Usada em dois lugares:
///   - morte do inimigo (<see cref="ExplodirEm"/> na posição dele);
///   - impacto de projétil (<see cref="ExplodirEm"/> com escala pequena).
///
/// Por que por código e não um prefab: o projeto não tinha nenhum sistema de explosão, e um
/// prefab a mais seria mais uma coisa para arrastar no Inspector. Aqui o sistema é montado
/// inteiro em <see cref="Awake"/>, então qualquer script dispara com uma linha.
///
/// <para>
/// Duas armadilhas que este arquivo evita de propósito:
/// </para>
/// <list type="bullet">
/// <item>O <c>Awake</c> roda <b>antes</b> de <see cref="ExplodirEm"/> configurar os campos, então
/// a montagem acontece no <see cref="Start"/>. Montar no Awake geraria uma explosão com os
/// valores padrão, ignorando cor e tamanho pedidos.</item>
/// <item>O material de partícula é compartilhado e criado uma única vez. Criar um
/// <c>new Material</c> por explosão vazaria memória numa partida longa.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Efeitos/Explosao De Particulas")]
public sealed class ExplosaoDeParticulas : MonoBehaviour
{
    /// <summary>
    /// Material único para todas as explosões. O sistema usa <c>startColor</c> para a cor,
    /// então um material branco neutro serve para todas.
    /// </summary>
    private static Material materialCompartilhado;

    [Header("Aparência")]
    [SerializeField] private Color cor = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField, Min(1)] private int quantidade = 40;
    [SerializeField, Min(0.1f)] private float raio = 1.4f;
    [SerializeField, Min(0.01f)] private float tamanho = 0.6f;
    [SerializeField, Min(0.1f)] private float duracao = 0.9f;

    [Header("Física")]
    [Tooltip("Velocidade inicial das partículas, em m/s.")]
    [SerializeField, Min(0f)] private float velocidade = 8f;

    [Tooltip("Gravidade aplicada às partículas (negativo cai).")]
    [SerializeField] private float gravidade = -6f;

    /// <summary>
    /// Cria uma explosão na posição indicada.
    ///
    /// <paramref name="escala"/> multiplica quantidade, tamanho e velocidade. Use 1 para a
    /// morte do inimigo e algo como 0,4 para o impacto do projétil, que é rápido e frequente
    /// (um tiro por segundo com explosão grande cobre justamente a mira).
    /// </summary>
    public static ExplosaoDeParticulas ExplodirEm(Vector3 posicao, Color cor, float escala = 1f)
    {
        GameObject objeto = new GameObject("Explosao");
        objeto.transform.position = posicao;

        ExplosaoDeParticulas explosao = objeto.AddComponent<ExplosaoDeParticulas>();

        // Sobrescreve os padrões ANTES do Start rodar (AddComponent não chama Start na hora).
        float fator = Mathf.Max(0.1f, escala);
        explosao.cor = cor;
        explosao.quantidade = Mathf.Max(1, Mathf.RoundToInt(explosao.quantidade * fator));
        explosao.tamanho *= fator;
        explosao.velocidade *= fator;
        explosao.raio *= fator;

        return explosao;
    }

    private void Start()
    {
        // Start, não Awake: aqui os campos já foram ajustados por ExplodirEm.
        ParticleSystem sistema = GetComponent<ParticleSystem>();
        if (sistema == null) sistema = gameObject.AddComponent<ParticleSystem>();

        // O AddComponent cria o sistema JÁ EM REPRODUÇÃO (ele toca sozinho no mesmo frame).
        // Configurar em seguida dispara o aviso "Setting the duration while system is still
        // playing is not supported". Limpar antes resolve: StopEmittingAndClear mata as
        // partículas e reseta o estado, então os módulos aceitam ser escritos.
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Configurar(sistema);
        sistema.Play(true);
    }

    /// <summary>
    /// Monta o sistema: irrupção única de partículas que saem em todas as direções, caem com
    /// gravidade e desaparecem. Os módulos do ParticleSystem são structs, então a única forma
    /// de alterá-los em runtime é pegar o módulo, mudar e deixar o setter aplicar.
    /// </summary>
    private void Configurar(ParticleSystem sistema)
    {
        ParticleSystem.MainModule principal = sistema.main;
        principal.duration = duracao;
        principal.loop = false;                       // sem loop, senão nunca morre
        principal.playOnAwake = false;
        principal.startLifetime = duracao;
        principal.startSpeed = velocidade;
        principal.startSize = tamanho;
        principal.startColor = cor;
        principal.gravityModifier = gravidade / 9.81f;  // o módulo espera múltiplo de g
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.stopAction = ParticleSystemStopAction.Destroy; // sem isso sobra GameObject
        principal.maxParticles = Mathf.Max(10, quantidade);

        ParticleSystem.EmissionModule emissao = sistema.emission;
        emissao.rateOverTime = 0f;                    // tudo de uma vez, na irrupção
        emissao.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)quantidade) });

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = Mathf.Max(0.05f, raio * 0.25f);

        // Nasce branco opaco (flash) e desaparece no fim.
        ParticleSystem.ColorOverLifetimeModule corAoLongo = sistema.colorOverLifetime;
        corAoLongo.enabled = true;
        corAoLongo.color = new ParticleSystem.MinMaxGradient(GradienteDeSumico());

        // Encolhe no fim, o que dá a sensação de apagar.
        ParticleSystem.SizeOverLifetimeModule tamanhoAoLongo = sistema.sizeOverLifetime;
        tamanhoAoLongo.enabled = true;
        tamanhoAoLongo.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        Renderer desenhista = sistema.GetComponent<Renderer>();
        if (desenhista != null)
        {
            desenhista.sharedMaterial = MaterialCompartilhado();
            desenhista.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            desenhista.receiveShadows = false;
        }
    }

    /// <summary>Material de partícula criado uma vez e reaproveitado por todas as explosões.</summary>
    private static Material MaterialCompartilhado()
    {
        if (materialCompartilhado != null) return materialCompartilhado;

        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) return null;

        materialCompartilhado = new Material(shader) { name = "Explosao (compartilhado)" };
        return materialCompartilhado;
    }

    /// <summary>Branco opaco → cor → transparente. O pico de branco dá o "flash".</summary>
    private Gradient GradienteDeSumico()
    {
        Gradient gradiente = new Gradient();

        gradiente.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(cor, 0.25f),
                new GradientColorKey(cor * 0.6f, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            });

        return gradiente;
    }
}
