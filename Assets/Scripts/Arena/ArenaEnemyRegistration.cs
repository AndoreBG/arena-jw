using UnityEngine;

/// <summary>
/// Conecta a morte da <see cref="Vida"/> ao spawner e ao placar: uma única vez, de forma idempotente.
/// Também libera a vaga quando o inimigo é destruído por outro motivo (sem pontuar).
/// </summary>
[RequireComponent(typeof(Vida))]
[DisallowMultipleComponent]
public sealed class ArenaEnemyRegistration : MonoBehaviour
{
    private Vida life;
    private ArenaSpawner spawner;
    private bool resolved;
    private AudioSource somDaNave;

    private void Awake()
    {
        life = GetComponent<Vida>();
    }

    /// <summary>
    /// O loop da nave alienígena começa junto com o inimigo. Fica no próprio GameObject,
    /// então para sozinho quando ele é destruído ao morrer.
    /// </summary>
    private void Start()
    {
        if (AudioDaArena.Instancia != null)
            somDaNave = AudioDaArena.Instancia.TocarNaveAlienigena(gameObject);
    }


    internal void Initialize(ArenaSpawner owner, ArenaManager arena)
    {
        spawner = owner;
        if (life == null) life = GetComponent<Vida>();
        if (life != null) life.DestruirAoMorrer = false;
    }

    private void OnEnable()
    {
        if (life == null) life = GetComponent<Vida>();
        if (life != null) life.AoMorrer += OnKilled;
    }

    private void OnDisable()
    {
        if (life != null) life.AoMorrer -= OnKilled;
    }

    private void OnKilled()
    {
        if (resolved) return;
        resolved = true;

        if (spawner != null) spawner.Unregister(this, true);

        // Para de andar e de atirar antes de sumir.
        foreach (InimigoMaquinaDeEstados maquina in GetComponentsInChildren<InimigoMaquinaDeEstados>())
            maquina.enabled = false;
        foreach (TiroDoInimigo tiro in GetComponentsInChildren<TiroDoInimigo>())
            tiro.enabled = false;

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // O loop da nave some junto com o inimigo; avisa o AudioDaArena para parar de
        // atualizar uma fonte que nao existe mais.
        if (AudioDaArena.Instancia != null)
            AudioDaArena.Instancia.EsquecerNave(somDaNave);

        if (!resolved && spawner != null)
        {
            resolved = true;
            spawner.Unregister(this, false);
        }
    }
}
