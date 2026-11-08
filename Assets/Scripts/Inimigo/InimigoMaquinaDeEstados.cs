using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Máquina de estados do inimigo, baseada na <c>InimigoMaquinaDeEstados</c> da Aula 05.
///
/// Três estados:
///   Andando     — vai em frente até trombar em algo; ao trombar, gira de 15° a 180° e segue
///   Perseguindo — vai atrás do jogador, fora do alcance de ataque
///   Atacando    — o jogador está dentro do alcance: para e atira
///
/// Quem decide é o peso de cada estado, vindo do <see cref="ConfiguracaoDoInimigo"/>:
/// quando mais de um estado é possível no mesmo frame, vence o de peso maior. Com pesos
/// iguais a prioridade é Atacando > Perseguindo > Andando.
///
/// Exceção: ao LEVAR DANO, o inimigo larga o que estava fazendo e passa a perseguir o
/// jogador, mesmo que não o enxergue (perseguição por dano). Ele fica indo atrás até
/// entrar no alcance de ataque e virar Atacando. Como perseguir para sempre travaria o
/// inimigo caso ele nunca alcance, existe o teto <see cref="tempoMaximoPerseguindo"/>.
///
/// O estado Andando não usa mais patrulha aleatória: o inimigo vai RETO para frente e,
/// quando tromba em alguma coisa, gira de 15° a 180° (sorteado) e segue em frente de novo.
///
/// Este script cuida de andar, enxergar e escolher o estado. Quem atira é o
/// <see cref="TiroDoInimigo"/>, no mesmo GameObject.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public sealed class InimigoMaquinaDeEstados : MonoBehaviour
{
    [Header("Estado atual (olhe no Inspector durante o jogo)")]
    public EstadoDoInimigo estadoAtual = EstadoDoInimigo.Andando;

    [Header("Configuração")]
    [Tooltip("Números do inimigo para a dificuldade. O spawner sobrescreve conforme a escolha do menu.")]
    [SerializeField] private ConfiguracaoDoInimigo configuracao;

    [Header("Referências")]
    [Tooltip("De onde o inimigo olha. Vazio = o próprio inimigo.")]
    [SerializeField] private Transform olhos;

    [Tooltip("Camadas que bloqueiam a visão (paredes). NÃO coloque a camada do jogador aqui.")]
    [SerializeField] private LayerMask camadasQueBloqueiamAVisao = ~0;

    [Tooltip("Altura do ponto mirado no jogador, a partir do pé dele.")]
    [SerializeField] private float alturaDoAlvo = 1f;

    [Header("Alvo")]
    [Tooltip("Se ficar vazio, procura pela tag.")]
    [SerializeField] private Transform jogador;
    [SerializeField] private string tagDoJogador = "Player";

    [Header("Andando (vai em frente até trombar)")]
    [Tooltip("Quantos metros o inimigo anda em linha reta antes de desistir e virar. " +
             "Evita ficar preso para sempre se o NavMesh acabar antes de bater em algo.")]
    [SerializeField, Min(2f)] private float distanciaMaximaReto = 60f;

    [Tooltip("Ângulo mínimo da virada, em graus, ao trombar em algo.")]
    [SerializeField, Range(15f, 179f)] private float giroMinimo = 15f;

    [Tooltip("Ângulo máximo da virada, em graus, ao trombar em algo.")]
    [SerializeField, Range(16f, 180f)] private float giroMaximo = 180f;

    [Header("Perseguindo")]
    [Tooltip("Tempo máximo perseguindo o jogador antes de voltar a andar, caso não o alcance. " +
             "Não se aplica enquanto o jogador estiver ao alcance de ataque.")]
    [SerializeField, Min(1f)] private float tempoMaximoPerseguindo = 20f;

    private NavMeshAgent agente;
    private float cronometroDoCaminho;

    /// <summary>True enquanto o inimigo está indo atrás por ter levado dano.</summary>
    private bool perseguicaoPorDano;

    /// <summary>Tempo que falta para desistir de perseguir (reiniciado a cada tiro que acerta).</summary>
    private float cronometroDaPerseguicao;

    /// <summary>Para onde o inimigo está indo no estado Andando.</summary>
    private Vector3 destinoDoAndar;


    /// <summary>Verdadeiro só no estado Atacando. O <see cref="TiroDoInimigo"/> lê isto.</summary>
    public bool PodeAtirar => estadoAtual == EstadoDoInimigo.Atacando;

    public Transform Jogador => jogador;

    /// <summary>Números da dificuldade em uso (o <see cref="TiroDoInimigo"/> lê daqui).</summary>
    public ConfiguracaoDoInimigo Configuracao => configuracao;

    /// <summary>O spawner chama isto logo depois de instanciar o inimigo.</summary>
    public void DefinirJogador(Transform alvo)
    {
        if (alvo != null) jogador = alvo;
    }

    /// <summary>O spawner chama isto com o asset da dificuldade escolhida no menu.</summary>
    public void DefinirConfiguracao(ConfiguracaoDoInimigo nova)
    {
        if (nova == null) return;
        configuracao = nova;
    }

    /// <summary>
    /// Chamado quando o inimigo leva dano. Ele larga o que estava fazendo e passa a perseguir
    /// o jogador, mesmo sem enxergá-lo. Persegue até entrar no alcance de ataque (vira Atacando)
    /// ou até o tempo de reação acabar.
    /// </summary>
    public void LevarDanoFazPerseguir()
    {
        if (jogador == null) return;

        // O dano pode ter matado o inimigo: 'Destroy' é adiado, então o componente ainda
        // executa neste frame, mas o NavMeshAgent já saiu do NavMesh. Mexer nele estoura
        // "Resume can only be called on an active agent".
        if (!EstaVivoENoNavMesh()) return;

        // Persegue sem prazo: o alvo só é esquecido quando ele for alcançado
        // (Atacando) ou quando o tempo de segurança geral vencer.
        perseguicaoPorDano = true;
        MudarDeEstado(EstadoDoInimigo.Perseguindo);
    }

    /// <summary>
    /// Falso quando o inimigo morreu (Vida.Morto), quando o componente foi desligado ou
    /// quando o NavMeshAgent não está mais em cima de um NavMesh. Nesses casos é proibido
    /// mandar o agente andar/parar.
    /// </summary>
    private bool EstaVivoENoNavMesh()
    {
        if (!enabled || !gameObject.activeInHierarchy) return false;
        if (agente == null || !agente.enabled || !agente.isOnNavMesh) return false;

        Vida vida = GetComponent<Vida>();
        if (vida != null && vida.Morto) return false;

        return true;
    }

    private void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        agente.updateRotation = false;
        agente.updateUpAxis = false;
        agente.autoBraking = false;

        if (olhos == null) olhos = transform;

        if (jogador == null)
        {
            GameObject objeto = GameObject.FindGameObjectWithTag(tagDoJogador);
            if (objeto != null) jogador = objeto.transform;
        }
    }

    private void Start()
    {
        EntrarNoEstado(estadoAtual);
    }

    private void Update()
    {
        if (configuracao == null) return;
        if (jogador == null || !agente.isOnNavMesh) return;

        bool enxerga = ConsegueVerOJogador();

        // Percebe = enxerga OU o jogador está perto (mesmo de costas). Usar uma única
        // noção de "percebe" em todos os estados evita ficar alternando entre eles.
        bool percebe = enxerga || JogadorPerto();

        // Levou dano: larga o que estava fazendo e vai atrás, enxergando ou não.
        if (perseguicaoPorDano && estadoAtual == EstadoDoInimigo.Andando)
            MudarDeEstado(EstadoDoInimigo.Perseguindo);

        switch (estadoAtual)
        {
            case EstadoDoInimigo.Andando:     AtualizarAndando(percebe); break;
            case EstadoDoInimigo.Perseguindo: AtualizarPerseguindo(percebe); break;
            case EstadoDoInimigo.Atacando:    AtualizarAtacando(percebe); break;
        }
    }

    // ---------------------------------------------------------------- escolha do estado

    /// <summary>
    /// Só os estados possíveis no momento entram na disputa. Vence o de peso maior; no empate,
    /// a ordem testada (Atacando, depois Perseguindo, depois Andando) já dá a prioridade certa.
    /// </summary>
    private EstadoDoInimigo EscolherEstado(bool enxerga = false, bool enxergarComoSeVisse = false)
    {
        bool ve = enxerga || enxergarComoSeVisse;

        bool podeAtacar = ve && DistanciaHorizontal() <= configuracao.AlcanceDeAtaque;
        bool podePerseguir = ve;

        EstadoDoInimigo melhor = EstadoDoInimigo.Andando;
        int melhorPeso = configuracao.PesoAndando;

        if (podePerseguir && configuracao.PesoPerseguindo >= melhorPeso)
        {
            melhor = EstadoDoInimigo.Perseguindo;
            melhorPeso = configuracao.PesoPerseguindo;
        }

        if (podeAtacar && configuracao.PesoAtacando >= melhorPeso)
        {
            melhor = EstadoDoInimigo.Atacando;
            melhorPeso = configuracao.PesoAtacando;
        }

        return melhor;
    }

    private void MudarDeEstado(EstadoDoInimigo novo)
    {
        estadoAtual = novo;
        EntrarNoEstado(novo);
    }

    /// <summary>O que acontece UMA VEZ, ao entrar no estado.</summary>
    private void EntrarNoEstado(EstadoDoInimigo estado)
    {
        // Sem isso, mexer no agente de um inimigo que acabou de morrer estoura
        // "Resume can only be called on an active agent that has been placed on a NavMesh".
        if (!EstaVivoENoNavMesh()) return;

        switch (estado)
        {
            case EstadoDoInimigo.Andando:
                agente.isStopped = false;
                agente.speed = configuracao.VelocidadeAndando;
                ComecarAAndarEmFrente();
                break;

            case EstadoDoInimigo.Perseguindo:
                agente.isStopped = false;
                agente.speed = configuracao.VelocidadePerseguindo;
                cronometroDoCaminho = 0f;
                cronometroDaPerseguicao = tempoMaximoPerseguindo;
                break;

            case EstadoDoInimigo.Atacando:
                agente.isStopped = true;
                agente.ResetPath();
                break;
        }
    }

    // ---------------------------------------------------------------- o que cada estado faz

    private void AtualizarAndando(bool enxerga)
    {
        if (enxerga)
        {
            MudarDeEstado(EscolherEstado(enxerga));
            return;
        }

        // Trombei em algo (parede, caixa, outro inimigo) ou acabei o NavMesh:
        // giro de 15° a 180° e sigo em frente de novo.
        if (ChegouAoDestino() || SemCaminho())
        {
            VirarAleatorio();
            return;
        }

        // Rede de segurança: se andar muito tempo sem bater em nada, vira também.
        if (DistanciaDoDestino() > distanciaMaximaReto)
            VirarAleatorio();
    }

    private void AtualizarPerseguindo(bool enxerga)
    {
        // Alcançou o jogador: vira Atacando (e para de atrás dele).
        // Aqui o alvo já está perto, então atacar vale mesmo sem estar no cone de visão
        // (senão ele chegava em cima do jogador e voltava a "Andando", piscando).
        if (DistanciaHorizontal() <= configuracao.AlcanceDeAtaque)
        {
            perseguicaoPorDano = false;
            MudarDeEstado(EstadoDoInimigo.Atacando);
            return;
        }

        // Só desiste se NÃO foi provocado por dano e perdeu o jogador de vista.
        if (!enxerga && !perseguicaoPorDano)
        {
            MudarDeEstado(EstadoDoInimigo.Andando);
            return;
        }

        // Tempo de segurança: perseguir para sempre nunca deixa o inimigo
        // voltar ao comportamento normal se ele não conseguir chegar.
        cronometroDaPerseguicao -= Time.deltaTime;
        if (cronometroDaPerseguicao <= 0f)
        {
            perseguicaoPorDano = false;
            MudarDeEstado(EstadoDoInimigo.Andando);
            return;
        }

        cronometroDoCaminho -= Time.deltaTime;
        if (cronometroDoCaminho <= 0f)
        {
            cronometroDoCaminho = 0.2f;
            agente.SetDestination(jogador.position);
        }
    }

    private void AtualizarAtacando(bool enxerga)
    {
        // A margem de 20% evita ficar trocando de estado sem parar na beirada do alcance.
        if (!enxerga || DistanciaHorizontal() > configuracao.AlcanceDeAtaque * 1.2f)
        {
            MudarDeEstado(EscolherEstado(enxerga));
            return;
        }

        OlharParaOJogador();
    }

    // ---------------------------------------------------------------- ajudantes

    /// <summary>
    /// Estado Andando: escolhe um ponto bem à frente e vai reto até lá. Ao trombar em algo,
    /// <see cref="VirarAleatorio"/> gira e chama este método de novo.
    /// </summary>
    private void ComecarAAndarEmFrente()
    {
        Vector3 frente = transform.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;

        Vector3 alvo = transform.position + frente.normalized * distanciaMaximaReto;

        if (NavMesh.SamplePosition(alvo, out NavMeshHit noChao, distanciaMaximaReto, NavMesh.AllAreas))
            destinoDoAndar = noChao.position;
        else
            destinoDoAndar = alvo;   // se não achou NavMesh, tenta mesmo assim

        agente.isStopped = false;
        agente.SetDestination(destinoDoAndar);
    }

    /// <summary>
    /// Gira de 15° a 180° (sorteado) e recomeça a andar em frente.
    /// Chamado quando o inimigo tromba em alguma coisa.
    /// </summary>
    private void VirarAleatorio()
    {
        float minimo = Mathf.Min(giroMinimo, giroMaximo);
        float maximo = Mathf.Max(giroMinimo, giroMaximo);
        float angulo = Random.Range(minimo, maximo);

        // Sorteia para a esquerda ou para a direita.
        if (Random.value < 0.5f) angulo = -angulo;

        transform.Rotate(0f, angulo, 0f, Space.Self);

        ComecarAAndarEmFrente();
    }

    /// <summary>
    /// Trombei em algo? O NavMeshAgent para de perto quando o caminho acaba, então
    /// "sem caminho" (invalid path ou path partial) significa que tem parede à frente.
    /// </summary>
    private bool SemCaminho()
    {
        return agente.pathStatus == NavMeshPathStatus.PathInvalid
            || agente.pathStatus == NavMeshPathStatus.PathPartial
            || !agente.hasPath;
    }

    /// <summary>Distância que ainda falta para o fim do trecho reto.</summary>
    private float DistanciaDoDestino()
    {
        Vector3 diferenca = destinoDoAndar - transform.position;
        diferenca.y = 0f;
        return diferenca.magnitude;
    }

    private bool ChegouAoDestino()
    {
        return !agente.pathPending
            && agente.remainingDistance <= agente.stoppingDistance + 0.5f;
    }

    /// <summary>
    /// O jogador está perto o bastante para o inimigo notar, mesmo sem enxergar?
    /// Usa 70% da distância de visão: é a "percepção" além do cone de visão.
    /// A margem evita ficar trocando de estado a cada frame na beirada.
    /// </summary>
    private bool JogadorPerto()
    {
        return DistanciaHorizontal() <= configuracao.DistanciaDeVisao * 0.7f;
    }

    private float DistanciaHorizontal()
    {
        Vector3 diferenca = jogador.position - transform.position;
        diferenca.y = 0f;
        return diferenca.magnitude;
    }

    private void OlharParaOJogador()
    {
        Vector3 direcao = jogador.position - transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.01f) return;

        Quaternion desejada = Quaternion.LookRotation(direcao);
        transform.rotation = Quaternion.Slerp(transform.rotation, desejada, 10f * Time.deltaTime);
    }

    /// <summary>Visão da aula: distância, ângulo e parede no caminho.</summary>
    private bool ConsegueVerOJogador()
    {
        Vector3 origem = olhos.position;
        Vector3 pontoDoJogador = jogador.position + Vector3.up * alturaDoAlvo;

        Vector3 direcao = pontoDoJogador - origem;
        float distancia = direcao.magnitude;

        if (distancia > configuracao.DistanciaDeVisao) return false;

        float angulo = Vector3.Angle(olhos.forward, direcao);
        if (angulo > configuracao.AnguloDeVisao * 0.5f) return false;

        if (!Physics.Raycast(origem, direcao.normalized, out RaycastHit obstaculo, distancia,
                camadasQueBloqueiamAVisao, QueryTriggerInteraction.Ignore))
        {
            return true; // nada no caminho
        }

        // O raio pode bater no próprio inimigo (o Corpo dele) ou no jogador. Nenhum dos dois é parede.
        Transform raizDoQueFoiAtingido = obstaculo.collider.transform.root;
        return raizDoQueFoiAtingido == jogador.root || raizDoQueFoiAtingido == transform.root;
    }

    // ---------------------------------------------------------------- editor

    private void OnDrawGizmosSelected()
    {
        if (configuracao == null) return;

        Transform origem = olhos != null ? olhos : transform;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origem.position, configuracao.DistanciaDeVisao);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, configuracao.AlcanceDeAtaque);
    }
}
