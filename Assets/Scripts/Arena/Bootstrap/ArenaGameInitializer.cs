using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara a partida do Playground sem exigir configuração manual na cena:
///
///  1. Aplica as configurações salvas (volumes e resolução).
///  2. Garante que exista um <see cref="ArenaManager"/> na cena.
///  3. Garante o HUD (<see cref="HUDController"/>) e o painel de Game Over
///     (<see cref="ArenaGameOverUI"/>), montados em código.
///  4. Usa os componentes que já estão na cena (vida do jogador, movimento, input) e cria os
///     que faltam (spawner, pool de projéteis).
///
/// O prefab <c>Assets/Prefabs/ArenaGame.prefab</c> carrega este componente e os dois painéis de
/// UI, para a cena ficar configurada de verdade (não só em runtime). O prefab é colocado na
/// cena pelo menu <c>Arena &gt; Setup Main Menu + Playground</c>; se ele não estiver lá, o
/// componente se auto-instala ao carregar o Playground.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Bootstrap/Arena Game Initializer")]
public sealed class ArenaGameInitializer : MonoBehaviour
{
    public const string PlaygroundSceneName = "playground";

    [Header("Assets")]
    [SerializeField] private DifficultyCatalog difficultyCatalog;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private SpawnerConfig spawnerConfig;

    [Header("Cenas")]
    [Tooltip("Instala o jogo automaticamente ao carregar o Playground.")]
    [SerializeField] private bool autoInstallOnPlayground = true;

    [Header("UI pronta (opcional)")]
    [Tooltip("HUD já existente na cena. Se vazio, é montado em código.")]
    [SerializeField] private HUDController hud;
    [Tooltip("Painel de Game Over já existente na cena. Se vazio, é montado em código.")]
    [SerializeField] private ArenaGameOverUI gameOver;

    [Header("Diagnóstico")]
    [SerializeField] private bool logReport = true;

    private void Awake()
    {
        if (!autoInstallOnPlayground) return;
        if (!string.Equals(SceneManager.GetActiveScene().name, PlaygroundSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        Install();
    }

    /// <summary>
    /// Garante um EventSystem compatível com o Input System. Sem isso, o campo de apelido do
    /// Game Over e os botões do menu não recebem clique.
    /// </summary>
    public static UnityEngine.EventSystems.EventSystem EnsureEventSystem()
    {
        UnityEngine.EventSystems.EventSystem current = UnityEngine.EventSystems.EventSystem.current;
        if (current != null) return current;

        GameObject go = new GameObject("EventSystem");
        UnityEngine.EventSystems.EventSystem system = go.AddComponent<UnityEngine.EventSystems.EventSystem>();

        // O projeto usa o Input System (activeInputHandler = 1): o módulo antigo não funciona.
        if (go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        return system;
    }

    /// <summary>Monta e liga tudo o que a partida precisa. Idempotente.</summary>
    public void Install()
    {
        GameSettings.ApplyRuntimeSettings();
        EnsureEventSystem();

        if (difficultyCatalog == null) difficultyCatalog = DifficultyCatalogLocator.Find();
        if (playerStats == null) playerStats = Resources.Load<PlayerStats>("PlayerStats");
        if (spawnerConfig == null) spawnerConfig = Resources.Load<SpawnerConfig>("SpawnerConfig");

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // 1. Dependências primeiro.
        ArenaSpawner spawner = FindFirstObjectByType<ArenaSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObject = new GameObject("ArenaSpawner");
            spawner = spawnerObject.AddComponent<ArenaSpawner>();
        }

        if (hud == null) hud = FindFirstObjectByType<HUDController>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject("HUDController");
            hud = hudObject.AddComponent<HUDController>();
            hud.BuildHierarchy();
        }

        if (gameOver == null) gameOver = FindFirstObjectByType<ArenaGameOverUI>();
        if (gameOver == null)
        {
            GameObject panelObject = new GameObject("GameOverUI");
            gameOver = panelObject.AddComponent<ArenaGameOverUI>();
            gameOver.BuildHierarchy();
        }

        // 2. ArenaManager por último, já com tudo pronto.
        ArenaManager manager = FindFirstObjectByType<ArenaManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("ArenaManager");
            manager = managerObject.AddComponent<ArenaManager>();
        }

        manager.Configure(difficultyCatalog, playerStats, spawner, hud, gameOver);

        EnsureProjectilePool(player);
        EnsureEnemySensorDefaults();

        if (player != null)
        {
            Vida life = player.GetComponentInParent<Vida>();
            TankMovement movement = player.GetComponent<TankMovement>();
            TankPlayerInput input = player.GetComponent<TankPlayerInput>();
            if (hud != null) hud.Bind(life, movement, input, manager);
        }

        if (logReport) Report(manager, player);
    }

    private void EnsureProjectilePool(GameObject player)
    {
        if (FindFirstObjectByType<TankProjectilePool>() != null) return;

        TankPlayerInput input = player != null ? player.GetComponent<TankPlayerInput>() : null;
        if (input != null && input.ProjectilePool != null) return;

        TankProjectile prefab = Resources.Load<TankProjectile>("ProjetilTanque");
        if (prefab == null)
        {
            Debug.LogWarning("[ArenaGameInitializer] Prefab do projétil não encontrado em Resources. " +
                             "O TankPlayerInput ainda pode criar a pool se estiver com o prefab configurado.");
            return;
        }

        TankProjectilePool.Ensure(prefab, 32);
    }

    /// <summary>O sensor nasce com "Everything"; paredes e o próprio jogador precisam ser ignorados.</summary>
    private static void EnsureEnemySensorDefaults()
    {
        foreach (ArenaEnemySensor sensor in FindObjectsByType<ArenaEnemySensor>(FindObjectsSortMode.None))
            if (sensor.BlockingMask == ~0) sensor.SetBlockingMask(DefaultLineOfSightMask);
    }

    /// <summary>Tudo menos "Ignore Raycast" (2), "Water" (4), "UI" (5) e o layer do jogador.</summary>
    public static int DefaultLineOfSightMask
    {
        get
        {
            int mask = ~0;
            mask &= ~(1 << 2);
            mask &= ~(1 << 5);
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) mask &= ~(1 << playerLayer);
            return mask;
        }
    }

    private static void Report(ArenaManager manager, GameObject player)
    {
        string difficulty = manager != null && manager.Difficulty != null ? manager.Difficulty.DisplayName : "---";
        Debug.Log("<b>[Arena] Partida configurada.</b>\n" +
                  "• Jogador: " + (player != null ? player.name : "<b>NÃO ENCONTRADO</b>") + "\n" +
                  "• Dificuldade: " + difficulty + (manager != null ? $" (limite {manager.EnemyLimit}, spawn {manager.SpawnInterval:0.0}s)" : "") + "\n" +
                  "• Spawner: " + (FindFirstObjectByType<ArenaSpawner>() != null ? "ok" : "<b>ausente</b>") +
                  " · Pool: " + (FindFirstObjectByType<TankProjectilePool>() != null ? "ok" : "<b>ausente</b>") + "\n" +
                  "• HUD: " + (FindFirstObjectByType<HUDController>() != null ? "ok" : "<b>ausente</b>") +
                  " · Game Over: " + (FindFirstObjectByType<ArenaGameOverUI>() != null ? "ok" : "<b>ausente</b>"));
    }

    /// <summary>
    /// Rede de segurança: se o prefab não estiver na cena e o Playground for carregado, instala
    /// o jogo automaticamente. (O caminho normal é o prefab, colocado pelo menu do Editor.)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstallOnSceneLoad()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!string.Equals(scene.name, PlaygroundSceneName, System.StringComparison.OrdinalIgnoreCase)) return;
        if (FindFirstObjectByType<ArenaGameInitializer>() != null) return;
        if (FindFirstObjectByType<ArenaManager>() != null && FindFirstObjectByType<ArenaGameOverUI>() != null) return;

        GameObject go = new GameObject("ArenaGameBootstrap");
        go.AddComponent<ArenaGameInitializer>().Install();
    }
}
