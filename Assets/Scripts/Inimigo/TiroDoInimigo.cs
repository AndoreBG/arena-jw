using UnityEngine;

/// <summary>
/// Disparo do inimigo.
///
/// O tiro nasce na posição central do GameObject <c>Corpo</c> do inimigo e vai em direção ao
/// centro do BoxCollider do <c>Corpo</c> do jogador.
///
/// Só atira quando o <see cref="InimigoMaquinaDeEstados"/> está no estado Atacando.
/// O projétil é o mesmo prefab do jogador (<c>ProjetilTanque</c>); velocidade, tempo de vida e
/// dano vêm do <see cref="ConfiguracaoDoInimigo"/> da dificuldade.
/// </summary>
[DisallowMultipleComponent]
public sealed class TiroDoInimigo : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("A máquina de estados. Vazio = procura neste GameObject.")]
    [SerializeField] private InimigoMaquinaDeEstados maquina;

    [Tooltip("De onde o projétil sai: o GameObject Corpo do inimigo.")]
    [SerializeField] private Transform origemDoTiro;

    [Tooltip("Prefab do projétil. Vazio = procura o ProjetilTanque no projeto.")]
    [SerializeField] private TankProjectile prefabDoProjetil;

    [Tooltip("Pool de projéteis da cena. Vazio = usa a pool que já existe.")]
    [SerializeField] private TankProjectilePool pool;

    [Header("Alvo")]
    [Tooltip("Onde o tiro mira: o BoxCollider do Corpo do jogador. Vazio = procura pelo nome.")]
    [SerializeField] private BoxCollider alvo;

    private float cronometro;

    /// <summary>Quantos tiros já saíram. Útil para depurar.</summary>
    public int TirosDados { get; private set; }

    private void Awake()
    {
        if (maquina == null) maquina = GetComponent<InimigoMaquinaDeEstados>();
        if (pool == null) pool = TankProjectilePool.Instance;
        if (origemDoTiro == null) origemDoTiro = transform;
    }

    private void Update()
    {
        if (maquina == null || !maquina.PodeAtirar) return;

        cronometro -= Time.deltaTime;
        if (cronometro > 0f) return;

        if (!TemTudoParaAtirar()) return;

        Atirar();
    }

    private bool TemTudoParaAtirar()
    {
        if (maquina.Jogador == null) return false;
        if (prefabDoProjetil == null) return false;
        if (pool == null) return false;

        if (alvo == null) alvo = ProcurarCorpoDoJogador(maquina.Jogador);
        return alvo != null;
    }

    private void Atirar()
    {
        ConfiguracaoDoInimigo cfg = maquina.Configuracao;
        if (cfg == null) return;

        Vector3 origem = origemDoTiro.position;
        Vector3 destino = alvo.bounds.center;
        Vector3 direcao = (destino - origem).normalized;

        cronometro = cfg.IntervaloEntreTiros;

        TankShotData tiro = new TankShotData(gameObject, TankShotFaction.Enemy, cfg.DanoDoProjetil);

        TankProjectile projetil = pool.RequestProjectile(
            origem, direcao,
            cfg.VelocidadeDoProjetil,
            cfg.TempoDeVidaDoProjetil,
            GetComponentsInChildren<Collider>(true),
            tiro,
            out _);

        if (projetil != null) TirosDados++;
    }

    /// <summary>
    /// O alvo é o BoxCollider do GameObject chamado "Corpo" dentro do jogador.
    /// O jogador do projeto é montado como Tanque > Corpo > ...
    /// </summary>
    private static BoxCollider ProcurarCorpoDoJogador(Transform jogador)
    {
        foreach (BoxCollider caixa in jogador.GetComponentsInChildren<BoxCollider>())
        {
            if (caixa.name == "Corpo") return caixa;
        }

        // Sem um filho chamado "Corpo": usa o primeiro BoxCollider que encontrar.
        return jogador.GetComponentInChildren<BoxCollider>();
    }
}
