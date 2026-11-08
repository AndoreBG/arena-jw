using UnityEngine;

/// <summary>
/// Casca de energia em volta do tanque, acionada por dano.
///
/// O <see cref="ArenaGameInitializer"/> cria este componente no jogador sem precisar de nada
/// montado na mão: ele gera uma esfera com o material do shader autoral
/// <c>Arena/EscudoDoTanque</c> e liga/desliga conforme o tanque leva dano.
///
/// O visual (grade, contorno, pulsação) é todo do shader. Este script só decide:
///   - quando acender (ao levar dano) e por quanto tempo;
///   - quão forte (proporcional ao tamanho do dano);
///   - a cor, que esquenta para o vermelho conforme a vida cai.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Efeitos/Escudo Do Tanque")]
public sealed class EscudoDoTanque : MonoBehaviour
{
    private const string NomeDoObjeto = "EscudoDoTanque";
    private const string NomeDaCasca = "Casca";
    private const string NomeDoShader = "Arena/EscudoDoTanque";

    [Header("Duração do flash")]
    [Tooltip("Quanto tempo o escudo fica aceso após um dano, em segundos.")]
    [SerializeField, Min(0.05f)] private float duracaoDoFlash = 0.55f;

    [Header("Cores")]
    [Tooltip("Cor com a vida cheia.")]
    [SerializeField] private Color corVidaCheia = new Color(0.25f, 0.75f, 1f, 1f);

    [Tooltip("Cor com a vida no fim (aviso de perigo).")]
    [SerializeField] private Color corVidaBaixa = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Forma")]
    [Tooltip("Diâmetro da casca em relação ao maior lado do tanque. 1 = encostada, 1.1 = folga.")]
    [SerializeField, Range(0.8f, 2f)] private float folgaDaCasca = 1.1f;

    [Tooltip("Deslocamento do centro da casca, em unidades LOCAIS do tanque. " +
             "Z negativo empurra para trás (na direção oposta ao canhão); Z positivo, para frente. " +
             "Use (0, 0, -2) para o centro 2 unidades atrás do centro calculado.")]
    [SerializeField] private Vector3 deslocamentoDoCentro = Vector3.zero;

    [Header("Montagem na cena (preenchido pela ferramenta Arena > Efeitos)")]
    [Tooltip("Material do shader. Se ficar vazio, o componente procura Arena/EscudoDoTanque.")]
    [SerializeField] private Material materialDoEscudo;

    [Tooltip("Renderer da casca que já existe na cena. Se ficar vazio, procura o filho 'Casca'.")]
    [SerializeField] private Renderer cascaDaCena;

    private Material material;
    private Renderer casca;
    private Vida vida;
    private float apagandoEm;
    private bool montado;
    private bool materialInstanciado;
    private int vezesAceso;

    /// <summary>
    /// Cria a casca no tanque. Chamado pelo inicializador; idempotente.
    ///
    /// <para>
    /// <paramref name="tanque"/> nulo não é erro silencioso: significa que
    /// <c>FindGameObjectWithTag("Player")</c> não achou nada, e aí o escudo simplesmente não
    /// existe. Já aconteceu de o tanque não estar com a tag certa e o efeito nunca aparecer —
    /// por isso o aviso explícito, que aponta direto para a causa.
    /// </para>
    ///
    /// <para>
    /// Se também não houver <see cref="Vida"/> no tanque, avisa: sem Vida não há evento de dano
    /// e o escudo nunca acenderia.
    /// </para>
    /// </summary>
    public static EscudoDoTanque CriarNo(Transform tanque)
    {
        if (tanque == null)
        {
            Debug.LogWarning(
                $"[{nameof(EscudoDoTanque)}] Nenhum objeto com a tag 'Player' foi encontrado. " +
                "O escudo não será criado. Confira a tag no tanque da cena e se ela existe em " +
                "Project Settings > Tags and Layers.", null);
            return null;
        }

        EscudoDoTanque existente = tanque.GetComponentInChildren<EscudoDoTanque>(true);
        if (existente != null) return existente;

        if (tanque.GetComponentInChildren<Vida>(true) == null)
        {
            Debug.LogWarning(
                $"[{nameof(EscudoDoTanque)}] O objeto '{tanque.name}' não tem componente Vida " +
                "em si nem nos filhos. O escudo seria criado, mas nunca acenderia: ele reage ao " +
                "evento AoAlterarVida. Verifique onde o dano do jogador é aplicado.", tanque);
        }

        GameObject objeto = new GameObject(NomeDoObjeto);
        objeto.transform.SetParent(tanque, false);

        EscudoDoTanque criado = objeto.AddComponent<EscudoDoTanque>();

        // Um log único de confirmação. Custa nada e responde na hora "o escudo existe?" —
        // foi a pergunta que ficou sem resposta na última rodada.
        Debug.Log($"[{nameof(EscudoDoTanque)}] Escudo criado em '{tanque.name}'.", tanque);

        return criado;
    }

    /// <summary>Material configurado no Inspector (usado pela ferramenta de Editor).</summary>
    public Material MaterialDoEscudo => materialDoEscudo;

    /// <summary>Deslocamento do centro, exposto para a ferramenta de Editor replicar a conta.</summary>
    public Vector3 DeslocamentoDoCentro => deslocamentoDoCentro;

    /// <summary>
    /// Liga o material e a casca que já vieram do Inspector (montagem feita no Editor).
    /// Chamado pela ferramenta de Editor; se nunca for chamado, o componente continua
    /// montando tudo sozinho em runtime como antes.
    /// </summary>
    public void Configurar(Material materialPronto)
    {
        if (materialPronto != null) materialDoEscudo = materialPronto;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnEnable()
    {
        if (vida == null) vida = GetComponentInParent<Vida>();
        if (vida != null)
        {
            vida.AoAlterarVida -= AoAlterarVida;
            vida.AoAlterarVida += AoAlterarVida;
            vida.AoMorrer -= AoMorrer;
            vida.AoMorrer += AoMorrer;
        }

        MontarCasca();
        Apagar();
    }

    private void OnDisable()
    {
        // Depois de destruído o Vida também vira null, então a desinscrição é feita com
        // cuidado: se ele já morreu, não há o que desinscrever.
        if (vida == null) return;
        vida.AoAlterarVida -= AoAlterarVida;
        vida.AoMorrer -= AoMorrer;
    }

    /// <summary>
    /// Cria a esfera e o material do shader. Se o shader não for encontrado (por exemplo em
    /// uma build que não incluiu o asset), o componente se desliga em vez de encher o console
    /// de erro a cada dano.
    /// </summary>
    private void MontarCasca()
    {
        if (montado) return;
        montado = true;

        // CAMINHO PREFERIDO: tudo já veio montado da cena/Inspector, nada é criado em runtime.
        if (materialDoEscudo != null || cascaDaCena != null)
        {
            if (material == null) material = materialDoEscudo;
            if (casca == null) casca = cascaDaCena != null ? cascaDaCena : ProcurarCasca();

            // O material da cena é um asset compartilhado: se o script escrevesse cor e
            // intensidade direto nele, o escudo ficaria aceso para sempre no PROJETO (o
            // .mat salvo mudaria, e o Git mostraria a alteração). Uma instância própria
            // resolve sem perder os valores ajustados no Inspector.
            //
            // A marca de "já instanciei" é uma flag, não o nome do material: em runtime o
            // asset pode vir sem nome e a checagem no nome estourava (NullReferenceException).
            if (material != null && !materialInstanciado)
            {
                string nomeOriginal = material.name;
                material = new Material(material);
                if (!string.IsNullOrEmpty(nomeOriginal)) material.name = nomeOriginal + " (Instance)";
                materialInstanciado = true;
            }

            if (casca != null && material != null) casca.sharedMaterial = material;

            if (casca == null)
            {
                Debug.LogWarning($"[{nameof(EscudoDoTanque)}] Nenhuma casca encontrada: nem " +
                                 "referência no Inspector, nem filho chamado 'Casca'. " +
                                 "Rode Arena > Efeitos > Montar Escudo do Tanque na Cena.", this);
            }

            return;
        }

        // CAMINHO ANTIGO (runtime): mantido só como rede de segurança para cenas que ainda não
        // receberam a montagem do Editor.
        Shader shader = Shader.Find(NomeDoShader);
        if (shader == null)
        {
            Debug.LogWarning($"[{nameof(EscudoDoTanque)}] Shader '{NomeDoShader}' não encontrado. " +
                             "O escudo fica desligado, mas o jogo funciona normalmente.", this);
            enabled = false;
            return;
        }

        material = new Material(shader) { name = "EscudoDoTanque (runtime)" };
        material.SetColor("_CorDoEscudo", corVidaCheia);

        // As medidas sao tiradas ANTES de criar a esfera: se fosse depois, o proprio escudo
        // entraria no calculo e inflaria o resultado.
        float diametro;
        Vector3 centro;
        MedirTanque(out diametro, out centro);

        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = NomeDaCasca;

        // A esfera fica FILHA do tanque para acompanhar o movimento, mas compensando a escala:
        // sem isso a escala do tanque era aplicada duas vezes (uma no diametro, outra herdada).
        // Num tanque com escala 0.5 a casca saia 4x menor que o calculo e o tanque aparecia por
        // fora dela — o efeito existia e nao se via nada. Compensar por eixo resolve o caso de
        // escala nao uniforme tambem.
        esfera.transform.SetParent(transform, false);

        // O deslocamento e' aplicado em espaco LOCAL do tanque (nao do mundo): assim, quando o
        // tanque gira, a casca deslocada gira junto e continua atras dele. Se fosse em mundo, o
        // escudo ficaria sempre para o mesmo lado da tela, "vazando" quando o tanque virasse.
        Vector3 centroComDeslocamento = centro + (transform.rotation * deslocamentoDoCentro);

        esfera.transform.localPosition = transform.InverseTransformPoint(centroComDeslocamento);
        esfera.transform.localScale = EscalaCompensada(diametro);

        // Só desenha: nenhum collider pode ficar no caminho dos tiros.
        Collider colisor = esfera.GetComponent<Collider>();
        if (colisor != null) Destroy(colisor);

        casca = esfera.GetComponent<Renderer>();
        if (casca != null)
        {
            casca.sharedMaterial = material;
            casca.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            casca.receiveShadows = false;
        }
    }

    /// <summary>Procura o filho 'Casca' na hierarquia do escudo.</summary>
    private Renderer ProcurarCasca()
    {
        Transform filha = transform.Find(NomeDaCasca);
        if (filha == null) return null;

        Renderer encontrado = filha.GetComponent<Renderer>();
        if (encontrado != null) encontrado.enabled = false; // nasce apagado
        return encontrado;
    }

    /// <summary>
    /// Mede o tanque e devolve o diametro da casca e o centro, ambos em unidades de MUNDO.
    ///
    /// Usa o maior lado do casco como referencia e aplica a folga: uma casca exatamente do
    /// tamanho do tanque corta as quinas na diagonal.
    /// </summary>
    private void MedirTanque(out float diametro, out Vector3 centro)
    {
        Transform raiz = transform.parent != null ? transform.parent : transform;

        Renderer[] partes = raiz.GetComponentsInChildren<Renderer>(true);
        bool temAlguma = false;
        Bounds area = default;

        for (int i = 0; i < partes.Length; i++)
        {
            Renderer parte = partes[i];
            if (parte == null) continue;

            // Ignora o proprio escudo (pode existir de uma tentativa anterior).
            if (parte.GetComponentInParent<EscudoDoTanque>() != null) continue;

            if (!temAlguma) { area = parte.bounds; temAlguma = true; }
            else area.Encapsulate(parte.bounds);
        }

        if (!temAlguma)
        {
            diametro = 4f;
            centro = transform.position;
            return;
        }

        float maiorLado = Mathf.Max(area.size.x, Mathf.Max(area.size.y, area.size.z));
        diametro = Mathf.Clamp(maiorLado * folgaDaCasca, 1f, 200f);
        centro = area.center;
    }

    /// <summary>
    /// Converte um diametro de mundo para localScale, dividindo pela escala herdada.
    ///
    /// A esfera primitiva nasce com 1 unidade de diametro, entao o localScale e' literalmente
    /// o tamanho desejado — desde que nao haja escala do pai. Como ha (o tanque pode estar
    /// escalado), divide-se eixo a eixo. Escala zero vira 1 para nao gerar escala infinita.
    /// </summary>
    private Vector3 EscalaCompensada(float diametroEmMundo)
    {
        Vector3 herdada = transform.lossyScale;

        float x = Mathf.Abs(herdada.x) > 0.0001f ? diametroEmMundo / Mathf.Abs(herdada.x) : diametroEmMundo;
        float y = Mathf.Abs(herdada.y) > 0.0001f ? diametroEmMundo / Mathf.Abs(herdada.y) : diametroEmMundo;
        float z = Mathf.Abs(herdada.z) > 0.0001f ? diametroEmMundo / Mathf.Abs(herdada.z) : diametroEmMundo;

        return new Vector3(x, y, z);
    }

    /// <summary>Ao levar dano: acende e aquece a cor conforme a vida cai.</summary>
    private void AoAlterarVida(float atual, float maxima)
    {
        // Unity "destrói" objetos nativamente: a referência vira null mesmo sem passar pelo
        // OnDisable. Sem esta checagem, o evento guardado no Vida estoura depois da morte.
        if (this == null || !isActiveAndEnabled) return;

        if (atual >= maxima) return; // cura/restauração não acende o escudo

        float proporcao = maxima > 0f ? Mathf.Clamp01(atual / maxima) : 0f;
        float forca = Mathf.Lerp(1f, 0.45f, proporcao); // quanto menos vida, mais forte

        Acender(forca, Color.Lerp(corVidaBaixa, corVidaCheia, proporcao));
    }

    private void AoMorrer()
    {
        Acender(1f, corVidaBaixa);
        apagandoEm = Time.unscaledTime + duracaoDoFlash * 1.6f;
    }

    private void Acender(float forca, Color cor)
    {
        if (material == null) return;

        // Conta os acendimentos: se o usuario levar dano e nao ver nada, a contagem prova se o
        // problema e' o evento (nao chega) ou o visual (chega e nao aparece).
        vezesAceso++;
        if (vezesAceso == 1)
        {
            Debug.Log($"[{nameof(EscudoDoTanque)}] Primeiro acendimento: casca em " +
                      $"'{casca?.name ?? "sem casca"}', diametro {(casca != null ? casca.transform.lossyScale.x : 0f):0.##} m, " +
                      $"intensidade {Mathf.Lerp(1.2f, 2.6f, forca):0.##}, cor {ColorUtility.ToHtmlStringRGB(cor)}. " +
                      "Se o escudo nao aparecer na tela, a causa e' visual, nao de evento.", this);
        }

        material.SetColor("_CorDoEscudo", cor);
        material.SetFloat("_Intensidade", Mathf.Lerp(1.2f, 2.6f, forca));
        material.SetFloat("_Opacidade", Mathf.Lerp(0.35f, 0.75f, forca));

        if (casca != null) casca.enabled = true;

        apagandoEm = Time.unscaledTime + duracaoDoFlash;
    }

    private void Apagar()
    {
        if (casca != null) casca.enabled = false;
    }

    private void Update()
    {
        // Tecla de teste temporaria: F2 simula o dano que o inimigo faria (25, o mesmo valor
        // do InimigoFacil). Existe porque esperar o inimigo mirar torna a verificacao do efeito
        // lenta e, pior, faz confundir "nao levou dano" com "escudo quebrado".
        if (TeclaDeTestePressionada()) SimularDano();

        if (casca == null || !casca.enabled) return;

        // Tempo não escalado: o escudo some normalmente mesmo com o jogo pausado no Game Over.
        if (Time.unscaledTime < apagandoEm) return;

        Apagar();
    }

    private bool teclaJaEstavaApertada;

    private bool TeclaDeTestePressionada()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard teclado = UnityEngine.InputSystem.Keyboard.current;
        if (teclado == null) return false;

        bool apertada = teclado[UnityEngine.InputSystem.Key.F2].isPressed;
        bool comecouAgora = apertada && !teclaJaEstavaApertada;
        teclaJaEstavaApertada = apertada;
        return comecouAgora;
#else
        return false;
#endif
    }

    /// <summary>Aplica dano de teste no tanque, passando pelo caminho real (Vida.ReceberDano).</summary>
    private void SimularDano()
    {
        if (vida == null) return;
        Debug.Log($"[{nameof(EscudoDoTanque)}] F2: aplicando 25 de dano de teste " +
                  $"({vida.VidaAtual:0}/{vida.VidaMaxima:0}).", this);
        vida.ReceberDano(25f);
    }

    private void OnDestroy()
    {
        // Material criado em runtime não é coletado sozinho enquanto existir referência.
        if (material != null) Destroy(material);
    }
}
