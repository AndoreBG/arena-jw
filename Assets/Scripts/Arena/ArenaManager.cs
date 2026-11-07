using UnityEngine;
using UnityEngine.SceneManagement;

public enum ArenaRunState { Playing, GameOver }

/// <summary>
/// Partida corrente: dificuldade, vida do jogador, pontuação, HUD e a transição (idempotente)
/// para o Game Over.
///
/// A dificuldade chega por PlayerPrefs (escolhida no painel do menu) e é resolvida contra o
/// <see cref="DifficultyCatalog"/>. Se nada estiver configurado, o componente usa o asset do
/// Inspector, para o Playground continuar funcionando quando aberto direto no Editor.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Arena Manager")]
public sealed class ArenaManager : MonoBehaviour
{
    public const string MenuSceneName = "MenuPrincipal";
    private static readonly string[] MenuSceneCandidates = { "MenuPrincipal", "menu_principal" };

    [Header("Dificuldade")]
    [Tooltip("Catálogo usado para resolver a dificuldade escolhida no menu.")]
    [SerializeField] private DifficultyCatalog difficultyCatalog;

    [Tooltip("Usada quando nenhum catálogo está configurado (útil para rodar o Playground direto).")]
    [SerializeField] private DifficultyConfig fallbackDifficulty;

    [Tooltip("Lê a dificuldade de PlayerPrefs (gravada pelo painel do menu) em vez de usar a do Inspector.")]
    [SerializeField] private bool useDifficultyFromPlayerPrefs = true;

    [Header("Jogador")]
    [SerializeField] private Vida playerLife;
    [SerializeField] private TankMovement playerMovement;
    [SerializeField] private TankPlayerInput playerInput;
    [SerializeField] private PlayerStats playerStats;

    [Header("Partida")]
    [SerializeField] private ArenaSpawner spawner;
    [SerializeField] private HUDController hud;
    [SerializeField] private ArenaGameOverUI gameOverUI;

    private int kills;
    private ArenaRunState state = ArenaRunState.Playing;
    private DifficultyConfig difficulty;

    public static ArenaManager Instance { get; private set; }
    public static bool IsPlaying => Instance == null || Instance.state == ArenaRunState.Playing;

    public ArenaRunState State => state;
    public int Kills => kills;
    public Vida PlayerLife => playerLife;
    public DifficultyConfig Difficulty => difficulty;
    public float SpawnInterval => difficulty != null ? difficulty.SpawnInterval : 3f;
    public int EnemyLimit => difficulty != null ? difficulty.MaxAliveEnemies : 2;
    public ArenaDifficulty CurrentDifficulty => difficulty != null ? difficulty.Difficulty : ArenaDifficulty.Facil;


    /// <summary>
    /// Liga as dependências antes do Awake. Chamado pelo <see cref="ArenaGameInitializer"/>, que
    /// pode criar este componente em runtime; nesse caso o Awake roda na hora (antes desta
    /// chamada) e a inicialização é repetida quando as referências chegam.
    /// </summary>
    public void Configure(DifficultyCatalog catalog, PlayerStats stats, ArenaSpawner arenaSpawner,
        HUDController hudController, ArenaGameOverUI gameOver)
    {
        bool changed = false;

        if (catalog != null && catalog != difficultyCatalog) { difficultyCatalog = catalog; changed = true; }
        if (stats != null && stats != playerStats) { playerStats = stats; changed = true; }
        if (arenaSpawner != null && arenaSpawner != spawner) { spawner = arenaSpawner; changed = true; }
        if (hudController != null && hudController != hud) { hud = hudController; changed = true; }
        if (gameOver != null && gameOver != gameOverUI) { gameOverUI = gameOver; changed = true; }

        // Sem mudanças, o Awake já fez o trabalho: não reinicializa (evita trabalho dobrado).
        if (changed || !awakeDone) Initialize();
    }

    private bool awakeDone;

    private void Awake()
    {
        awakeDone = true;
        Initialize();
    }

    private void Initialize()
    {
        Time.timeScale = 1f;
        Instance = this;

        ResolvePlayer();
        ResolveDifficulty();
        ApplyPlayerStats();

        if (playerLife != null)
        {
            playerLife.DestruirAoMorrer = false;
            playerLife.AoMorrer -= OnPlayerDied;
            playerLife.AoMorrer += OnPlayerDied;
        }
        else Debug.LogError("[ArenaManager] Vida do jogador não configurada nem encontrada na tag Player.", this);

        if (gameOverUI == null) gameOverUI = FindFirstObjectByType<ArenaGameOverUI>();
        if (hud == null) hud = FindFirstObjectByType<HUDController>();

        if (hud != null) hud.Bind(playerLife, playerMovement, playerInput, this);
        if (spawner == null) spawner = FindFirstObjectByType<ArenaSpawner>();
        if (spawner != null) spawner.SetManager(this);
    }


    private void OnDestroy()
    {
        if (playerLife != null) playerLife.AoMorrer -= OnPlayerDied;
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    private void ResolvePlayer()
    {
        if (playerLife == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerLife = player.GetComponentInParent<Vida>();
        }

        if (playerLife == null) return;

        if (playerMovement == null) playerMovement = playerLife.GetComponent<TankMovement>();
        if (playerInput == null) playerInput = playerLife.GetComponent<TankPlayerInput>();
    }

    private void ResolveDifficulty()
    {
        ArenaDifficulty wanted = useDifficultyFromPlayerPrefs ? GameSettings.Difficulty : ArenaDifficulty.Facil;

        if (difficultyCatalog == null)
            difficultyCatalog = DifficultyCatalogLocator.Find();

        if (difficultyCatalog != null)
            difficulty = difficultyCatalog.Get(wanted);

        if (difficulty == null) difficulty = fallbackDifficulty;

        if (difficulty == null)
            Debug.LogWarning("[ArenaManager] Nenhum DifficultyConfig disponível. " +
                             "Rode 'Arena > Create ScriptableObjects Assets' e configure o catálogo.", this);
    }

    private void ApplyPlayerStats()
    {
        if (playerStats == null) return;
        playerStats.ApplyTo(playerLife, playerMovement, playerInput);
    }

    public void RegisterEnemyKill(ArenaSpawner owner)
    {
        if (state != ArenaRunState.Playing) return;
        kills++;
        if (hud != null) hud.RefreshScore(kills);
    }

    private void OnPlayerDied()
    {
        if (state != ArenaRunState.Playing) return;
        state = ArenaRunState.GameOver;

        if (spawner != null) spawner.enabled = false;

        foreach (TankPlayerInput input in FindObjectsByType<TankPlayerInput>(FindObjectsSortMode.None)) input.enabled = false;
        foreach (TankMovement movement in FindObjectsByType<TankMovement>(FindObjectsSortMode.None)) movement.Stop();
        foreach (AlienShipAI ship in FindObjectsByType<AlienShipAI>(FindObjectsSortMode.None))
        {
            ship.enabled = false;
            UnityEngine.AI.NavMeshAgent agent = ship.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }
        foreach (ArenaEnemyAI legacy in FindObjectsByType<ArenaEnemyAI>(FindObjectsSortMode.None)) legacy.enabled = false;
        foreach (AlienShipWeapon weapon in FindObjectsByType<AlienShipWeapon>(FindObjectsSortMode.None)) weapon.SetCanFire(false);
        foreach (TankProjectile shot in FindObjectsByType<TankProjectile>(FindObjectsSortMode.None)) shot.RequestDespawn();

        GameSettings.SaveDifficulty(CurrentDifficulty);

        if (gameOverUI != null) gameOverUI.Show(kills, CurrentDifficulty);
        else Debug.LogError("[ArenaManager] GameOverUI não configurado.", this);

        if (hud != null) hud.SetGameOverVisible(true);

        Time.timeScale = 0f;
    }

    /// <summary>Recarrega a cena mantendo a mesma dificuldade.</summary>
    public void StartNewGame()
    {
        Time.timeScale = 1f;
        GameSettings.SaveDifficulty(CurrentDifficulty);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Volta para o menu principal, se ele estiver no Build Settings.</summary>
    public bool TryReturnToMenu(out string error)
    {
        error = null;
        Time.timeScale = 1f;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (System.Array.IndexOf(MenuSceneCandidates, name) >= 0)
            {
                SceneManager.LoadScene(i);
                return true;
            }
        }

        error = "A cena de menu não está no Build Settings. Adicione 'menu_principal' em File > Build Settings.";
        return false;
    }
}

/// <summary>Localiza o catálogo de dificuldades por Resources, caso ninguém o tenha atribuído.</summary>
public static class DifficultyCatalogLocator
{
    public const string ResourcesName = "DifficultyCatalog";

    public static DifficultyCatalog Find()
    {
        DifficultyCatalog catalog = Resources.Load<DifficultyCatalog>(ResourcesName);
        if (catalog != null) return catalog;
        return null;
    }
}
