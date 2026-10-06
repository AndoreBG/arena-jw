using UnityEngine;
using UnityEngine.AI;

// Os estados possíveis do inimigo. Ele está sempre em UM deles, nunca em dois.
public enum EstadoDoInimigo
{
    Patrulhando,
    Perseguindo,
    Procurando,
    Atacando
}

[RequireComponent(typeof(NavMeshAgent))]
public class InimigoMaquinaDeEstados : MonoBehaviour
{
    [Header("Estado atual (olhe no Inspector durante o jogo)")]
    public EstadoDoInimigo estadoAtual = EstadoDoInimigo.Patrulhando;

    [Header("Alvo")]
    public Transform jogador;                    // se ficar vazio, procura pela tag
    public string tagDoJogador = "Player";

    [Header("Visão")]
    public float distanciaDeVisao = 15f;
    [Range(0f, 360f)]
    public float anguloDeVisao = 90f;
    [Tooltip("De onde o inimigo olha (ex: cabeça). Se vazio, usa o próprio inimigo.")]
    public Transform olhos;
    [Tooltip("Camadas que bloqueiam a visão (paredes, obstáculos). NÃO coloque a camada do Player.")]
    public LayerMask camadasQueBloqueiamAVisao;
    public float alturaDoAlvo = 1.2f;            // mira no peito, não no pé do jogador

    [Header("Patrulha")]
    public float raioDePatrulha = 8f;
    public float velocidadeDePatrulha = 2f;

    [Header("Perseguição")]
    public float velocidadeDePerseguicao = 4.5f;
    public float intervaloParaRecalcularCaminho = 0.2f;   // não recalcula todo frame

    [Header("Procura")]
    public float tempoDeProcura = 3f;

    [Header("Ataque")]
    public float distanciaDeAtaque = 1.8f;
    public float intervaloEntreAtaques = 1f;
    public int danoPorAtaque = 1;

    private NavMeshAgent agenteDeNavegacao;
    private Vector3 posicaoInicial;
    private Vector3 ultimaPosicaoVista;
    private float cronometroDoCaminho;
    private float cronometroDaProcura;
    private float cronometroDoAtaque;

    void Awake()
    {
        agenteDeNavegacao = GetComponent<NavMeshAgent>();
        agenteDeNavegacao.stoppingDistance = distanciaDeAtaque * 0.5f;

        if (olhos == null) olhos = transform;

        if (jogador == null)
        {
            GameObject objetoDoJogador = GameObject.FindGameObjectWithTag(tagDoJogador);
            if (objetoDoJogador != null) jogador = objetoDoJogador.transform;
        }
    }

    void Start()
    {
        // O spawner da aula 5 coloca o inimigo em um lugar aleatório:
        // ele patrulha em volta de onde nasceu
        posicaoInicial = transform.position;
        EntrarNoEstado(estadoAtual);
    }

    void Update()
    {
        if (jogador == null || !agenteDeNavegacao.isOnNavMesh) return;

        bool consegueVerOJogador = ConsegueVerOJogador();
        if (consegueVerOJogador)
        {
            ultimaPosicaoVista = jogador.position;
        }

        // O coração da máquina de estados: cada estado cuida de si mesmo
        switch (estadoAtual)
        {
            case EstadoDoInimigo.Patrulhando:
                AtualizarPatrulha(consegueVerOJogador);
                break;
            case EstadoDoInimigo.Perseguindo:
                AtualizarPerseguicao(consegueVerOJogador);
                break;
            case EstadoDoInimigo.Procurando:
                AtualizarProcura(consegueVerOJogador);
                break;
            case EstadoDoInimigo.Atacando:
                AtualizarAtaque();
                break;
        }
    }

    // ---------- Trocar de estado ----------

    private void MudarDeEstado(EstadoDoInimigo novoEstado)
    {
        Debug.Log(name + ": " + estadoAtual + " -> " + novoEstado);
        estadoAtual = novoEstado;
        EntrarNoEstado(novoEstado);
    }

    // O que acontece UMA VEZ, no momento em que o inimigo entra no estado
    private void EntrarNoEstado(EstadoDoInimigo estado)
    {
        switch (estado)
        {
            case EstadoDoInimigo.Patrulhando:
                agenteDeNavegacao.isStopped = false;
                agenteDeNavegacao.speed = velocidadeDePatrulha;
                IrParaUmPontoAleatorio();
                break;

            case EstadoDoInimigo.Perseguindo:
                agenteDeNavegacao.isStopped = false;
                agenteDeNavegacao.speed = velocidadeDePerseguicao;
                cronometroDoCaminho = 0f;   // calcula o caminho já no primeiro frame
                break;

            case EstadoDoInimigo.Procurando:
                agenteDeNavegacao.isStopped = false;
                agenteDeNavegacao.speed = velocidadeDePatrulha;
                agenteDeNavegacao.SetDestination(ultimaPosicaoVista);
                cronometroDaProcura = tempoDeProcura;
                break;

            case EstadoDoInimigo.Atacando:
                agenteDeNavegacao.isStopped = true;
                cronometroDoAtaque = 0f;    // ataca assim que chega
                break;
        }
    }

    // ---------- O que cada estado faz a cada frame ----------

    private void AtualizarPatrulha(bool consegueVerOJogador)
    {
        if (consegueVerOJogador)
        {
            MudarDeEstado(EstadoDoInimigo.Perseguindo);
            return;
        }

        if (ChegouAoDestino())
        {
            IrParaUmPontoAleatorio();
        }
    }

    private void AtualizarPerseguicao(bool consegueVerOJogador)
    {
        if (!consegueVerOJogador)
        {
            MudarDeEstado(EstadoDoInimigo.Procurando);
            return;
        }

        if (DistanciaAteOJogador() <= distanciaDeAtaque)
        {
            MudarDeEstado(EstadoDoInimigo.Atacando);
            return;
        }

        cronometroDoCaminho -= Time.deltaTime;
        if (cronometroDoCaminho <= 0f)
        {
            cronometroDoCaminho = intervaloParaRecalcularCaminho;
            agenteDeNavegacao.SetDestination(jogador.position);
        }
    }

    private void AtualizarProcura(bool consegueVerOJogador)
    {
        if (consegueVerOJogador)
        {
            MudarDeEstado(EstadoDoInimigo.Perseguindo);
            return;
        }

        // Chegou onde viu o jogador pela última vez: fica olhando em volta
        if (ChegouAoDestino())
        {
            transform.Rotate(0f, 120f * Time.deltaTime, 0f);
            cronometroDaProcura -= Time.deltaTime;

            if (cronometroDaProcura <= 0f)
            {
                MudarDeEstado(EstadoDoInimigo.Patrulhando);
            }
        }
    }

    private void AtualizarAtaque()
    {
        // A margem de 20% evita ficar trocando de estado sem parar na beirada
        if (DistanciaAteOJogador() > distanciaDeAtaque * 1.2f)
        {
            MudarDeEstado(EstadoDoInimigo.Perseguindo);
            return;
        }

        OlharParaOJogador();

        cronometroDoAtaque -= Time.deltaTime;
        if (cronometroDoAtaque <= 0f)
        {
            cronometroDoAtaque = intervaloEntreAtaques;
            Atacar();
        }
    }

    private void Atacar()
    {
        Debug.Log(name + " atacou! Dano: " + danoPorAtaque);
        // Aqui entra o método da barra de vida da aula 2, por exemplo:
        // jogador.GetComponent<VidaDoJogador>().ReceberDano(danoPorAtaque);
    }

    // ---------- Ajudantes ----------

    private void IrParaUmPontoAleatorio()
    {
        Vector3 pontoSorteado = posicaoInicial + Random.insideUnitSphere * raioDePatrulha;

        // Ajusta o ponto sorteado para o lugar andável mais próximo
        if (NavMesh.SamplePosition(pontoSorteado, out NavMeshHit pontoNoChao, raioDePatrulha, NavMesh.AllAreas))
        {
            agenteDeNavegacao.SetDestination(pontoNoChao.position);
        }
    }

    private bool ChegouAoDestino()
    {
        return !agenteDeNavegacao.pathPending
            && agenteDeNavegacao.remainingDistance <= agenteDeNavegacao.stoppingDistance + 0.1f;
    }

    private float DistanciaAteOJogador()
    {
        return Vector3.Distance(transform.position, jogador.position);
    }

    private void OlharParaOJogador()
    {
        Vector3 direcaoDoJogador = jogador.position - transform.position;
        direcaoDoJogador.y = 0f;
        if (direcaoDoJogador.sqrMagnitude < 0.01f) return;

        Quaternion rotacaoDesejada = Quaternion.LookRotation(direcaoDoJogador);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoDesejada, 10f * Time.deltaTime);
    }

    // Visão reaproveitada do script antigo: distância, ângulo e parede no caminho
    private bool ConsegueVerOJogador()
    {
        Vector3 origemDoOlhar = olhos.position;
        Vector3 pontoDoJogador = jogador.position + Vector3.up * alturaDoAlvo;

        Vector3 direcaoDoOlhar = pontoDoJogador - origemDoOlhar;
        float distanciaAteOJogador = direcaoDoOlhar.magnitude;

        // 1. Está longe demais?
        if (distanciaAteOJogador > distanciaDeVisao) return false;

        // 2. Está fora do campo de visão?
        float anguloAteOJogador = Vector3.Angle(olhos.forward, direcaoDoOlhar);
        if (anguloAteOJogador > anguloDeVisao * 0.5f) return false;

        // 3. Tem parede no meio?
        bool temObstaculoNoMeio = Physics.Raycast(origemDoOlhar, direcaoDoOlhar.normalized,
            distanciaAteOJogador, camadasQueBloqueiamAVisao, QueryTriggerInteraction.Ignore);

        return !temObstaculoNoMeio;
    }

    // ---------- Desenhos no editor ----------

    // Bolinha colorida em cima do inimigo: mostra o estado durante o jogo (ligue os Gizmos)
    void OnDrawGizmos()
    {
        switch (estadoAtual)
        {
            case EstadoDoInimigo.Patrulhando: Gizmos.color = Color.green; break;
            case EstadoDoInimigo.Perseguindo: Gizmos.color = Color.yellow; break;
            case EstadoDoInimigo.Procurando:  Gizmos.color = Color.cyan; break;
            case EstadoDoInimigo.Atacando:    Gizmos.color = Color.red; break;
        }
        Gizmos.DrawSphere(transform.position + Vector3.up * 2.5f, 0.3f);
    }

    // Cone de visão e alcance de ataque, quando o inimigo está selecionado
    void OnDrawGizmosSelected()
    {
        Transform origemDoOlhar = olhos != null ? olhos : transform;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origemDoOlhar.position, distanciaDeVisao);

        Vector3 limiteEsquerdo = Quaternion.Euler(0f, -anguloDeVisao * 0.5f, 0f) * origemDoOlhar.forward;
        Vector3 limiteDireito = Quaternion.Euler(0f, anguloDeVisao * 0.5f, 0f) * origemDoOlhar.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origemDoOlhar.position, limiteEsquerdo * distanciaDeVisao);
        Gizmos.DrawRay(origemDoOlhar.position, limiteDireito * distanciaDeVisao);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaDeAtaque);
    }
}
