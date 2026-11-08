using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Máquina de estados do inimigo, baseada na <c>InimigoMaquinaDeEstados</c> da Aula 05.
///
/// Três estados:
///   Andando     — patrulha em volta de onde nasceu (não enxerga o jogador)
///   Perseguindo — enxerga o jogador, mas está fora do alcance de ataque
///   Atacando    — o jogador está dentro do alcance: para e atira
///
/// Quem decide é o peso de cada estado, vindo do <see cref="ConfiguracaoDoInimigo"/>:
/// quando mais de um estado é possível no mesmo frame, vence o de peso maior. Com pesos
/// iguais a prioridade é Atacando > Perseguindo > Andando.
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

    private NavMeshAgent agente;
    private Vector3 posicaoInicial;
    private float cronometroDoCaminho;

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
        posicaoInicial = transform.position;
        EntrarNoEstado(estadoAtual);
    }

    private void Update()
    {
        if (configuracao == null) return;
        if (jogador == null || !agente.isOnNavMesh) return;

        bool enxerga = ConsegueVerOJogador();

        switch (estadoAtual)
        {
            case EstadoDoInimigo.Andando:     AtualizarAndando(enxerga); break;
            case EstadoDoInimigo.Perseguindo: AtualizarPerseguindo(enxerga); break;
            case EstadoDoInimigo.Atacando:    AtualizarAtacando(enxerga); break;
        }
    }

    // ---------------------------------------------------------------- escolha do estado

    /// <summary>
    /// Só os estados possíveis no momento entram na disputa. Vence o de peso maior; no empate,
    /// a ordem testada (Atacando, depois Perseguindo, depois Andando) já dá a prioridade certa.
    /// </summary>
    private EstadoDoInimigo EscolherEstado(bool enxerga)
    {
        bool podeAtacar = enxerga && DistanciaHorizontal() <= configuracao.AlcanceDeAtaque;
        bool podePerseguir = enxerga;

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
        switch (estado)
        {
            case EstadoDoInimigo.Andando:
                agente.isStopped = false;
                agente.speed = configuracao.VelocidadeAndando;
                IrParaUmPontoAleatorio();
                break;

            case EstadoDoInimigo.Perseguindo:
                agente.isStopped = false;
                agente.speed = configuracao.VelocidadePerseguindo;
                cronometroDoCaminho = 0f;
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

        if (ChegouAoDestino()) IrParaUmPontoAleatorio();
    }

    private void AtualizarPerseguindo(bool enxerga)
    {
        if (!enxerga)
        {
            MudarDeEstado(EstadoDoInimigo.Andando);
            return;
        }

        if (DistanciaHorizontal() <= configuracao.AlcanceDeAtaque)
        {
            MudarDeEstado(EscolherEstado(enxerga));
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

    private void IrParaUmPontoAleatorio()
    {
        Vector3 ponto = posicaoInicial + Random.insideUnitSphere * configuracao.RaioDePatrulha;
        ponto.y = posicaoInicial.y;

        if (NavMesh.SamplePosition(ponto, out NavMeshHit noChao, configuracao.RaioDePatrulha, NavMesh.AllAreas))
        {
            agente.isStopped = false;
            agente.SetDestination(noChao.position);
        }
    }

    private bool ChegouAoDestino()
    {
        return !agente.pathPending
            && agente.remainingDistance <= agente.stoppingDistance + 0.5f;
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
