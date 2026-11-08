using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara a partida do Playground sem exigir configuração manual na cena:
///
///  1. Aplica as configurações salvas (volumes e resolução).
///  2. Garante que exista um <see cref="ArenaManager"/> na cena.
///  3. Usa o HUD (<see cref="HUDController"/>) e o painel de Game Over
///     (<see cref="ArenaGameOverUI"/>), que já vêm montados no prefab ArenaGame.
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
    [Tooltip("HUD que já existe no prefab/cena (HUD_Partida).")]
    [SerializeField] private HUDController hud;
    [Tooltip("Painel de Game Over que já existe no prefab/cena (GameOverPanel).")]
    [SerializeField] private ArenaGameOverUI gameOver;

    private void Awake()
    {
        if (!autoInstallOnPlayground) return;
        if (!string.Equals(SceneManager.GetActiveScene().name, PlaygroundSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        Install();
    }

    /// <summary>
    /// Deixa o menu utilizavel no controle. O Unity só navega com D-pad/stick quando existe
    /// um objeto selecionado no <see cref="UnityEngine.EventSystems.EventSystem"/>, e por
    /// padrão a cena abre sem nenhum.
    ///
    /// <para>
    /// A escolha do primeiro selecionável NÃO pode ser a ordem da hierarquia: no menu há
    /// botões de diálogos fechados (Sim/Não do "voltar ao menu") que vêm antes do "Jogar" e
    /// roubariam a seleção. Aqui filtra-se apenas o que está realmente visível e clicável e
    /// escolhe-se o mais acima na tela, que é o primeiro botão do painel aberto.
    /// </para>
    /// </summary>
    public static void EnsureMenuNavigable()
    {
        UnityEngine.EventSystems.EventSystem sistema = UnityEngine.EventSystems.EventSystem.current;
        if (sistema == null) return;

        // Já existe seleção válida e visível? Não mexe (não rouba a navegação de quem está
        // no meio dela). Se a seleção aponta para algo desligado, é lixo: troca.
        GameObject atual = sistema.currentSelectedGameObject;
        if (atual != null && atual.activeInHierarchy &&
            atual.GetComponent<UnityEngine.UI.Selectable>()?.IsInteractable() == true)
            return;

        UnityEngine.UI.Selectable primeiro = PrimeiroSelecionavel();
        if (primeiro != null) sistema.SetSelectedGameObject(primeiro.gameObject);
    }

    /// <summary>
    /// O botão mais acima na tela entre os visíveis, ativos e clicáveis. Ignora diálogos
    /// fechados, componentes desligados e painéis com o CanvasGroup transparente.
    /// </summary>
    public static UnityEngine.UI.Selectable PrimeiroSelecionavel()
    {
        UnityEngine.UI.Selectable melhor = null;
        float melhorOrdem = float.MaxValue;

        foreach (UnityEngine.UI.Selectable candidato in
                 FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsSortMode.None))
        {
            if (candidato == null || !candidato.IsInteractable()) continue;
            if (!candidato.isActiveAndEnabled) continue;
            if (!candidato.gameObject.activeInHierarchy) continue;
            if (!Visivel(candidato)) continue;

            float ordem = OrdemNaTela(candidato);
            if (ordem < melhorOrdem)
            {
                melhorOrdem = ordem;
                melhor = candidato;
            }
        }

        return melhor;
    }

    /// <summary>Falso quando algum CanvasGroup do caminho está com alpha 0.</summary>
    private static bool Visivel(UnityEngine.Component componente)
    {
        for (Transform t = componente.transform; t != null; t = t.parent)
        {
            UnityEngine.CanvasGroup grupo = t.GetComponent<UnityEngine.CanvasGroup>();
            if (grupo != null && grupo.alpha <= 0.01f) return false;
        }
        return true;
    }

    /// <summary>Ordem de leitura: mais para cima primeiro e, na mesma altura, da esquerda.</summary>
    private static float OrdemNaTela(UnityEngine.UI.Selectable candidato)
    {
        if (!(candidato.transform is RectTransform rt)) return float.MaxValue;
        Vector3 posicao = rt.position;
        return -posicao.y * 10000f + posicao.x;
    }

    /// <summary>
    /// Liga a navegação do menu ao controle: põe o <see cref="NavegacaoDoControle"/> no
    /// EventSystem (se ainda não estiver lá). Fica no EventSystem porque é o único objeto
    /// que toda cena com menu já tem — não precisa de nada por cena.
    /// </summary>
    public static void ConectarNavegacaoDoControle()
    {
        UnityEngine.EventSystems.EventSystem sistema = EnsureEventSystem();
        if (sistema == null) return;

        if (sistema.GetComponent<NavegacaoDoControle>() == null)
            sistema.gameObject.AddComponent<NavegacaoDoControle>();
    }

    /// <summary>
    /// Garante o <see cref="AudioDaArena"/> na cena: é ele que toca os efeitos (tank-shot,
    /// alien-shot, alien-ship) e a música de fundo.
    /// </summary>
    public static AudioDaArena EnsureAudio()
    {
        AudioDaArena atual = AudioDaArena.Instancia;
        if (atual == null) atual = FindFirstObjectByType<AudioDaArena>();

        if (atual != null) return atual;

        GameObject go = new GameObject("AudioDaArena");
        return go.AddComponent<AudioDaArena>();
    }

    /// <summary>
    /// Garante o atalho de saída (segurar Esc volta ao menu sem gravar pontuação).
    /// </summary>
    public static AtalhoParaOMenu EnsureAtalhoParaOMenu()
    {
        AtalhoParaOMenu atual = FindFirstObjectByType<AtalhoParaOMenu>();
        if (atual != null) return atual;

        GameObject go = new GameObject("AtalhoParaOMenu");
        return go.AddComponent<AtalhoParaOMenu>();
    }

    /// <summary>
    /// Garante um EventSystem compatível com o Input System. Sem isso, o campo de apelido do
    /// Game Over e os botões do menu não recebem clique.
    /// </summary>
    public static UnityEngine.EventSystems.EventSystem EnsureEventSystem()
    {
        UnityEngine.EventSystems.EventSystem current = UnityEngine.EventSystems.EventSystem.current;
        if (current != null)
        {
            // Já existe EventSystem na cena: só garante que ele navegue no controle.
            // (Idempotente; roda no Awake do MenuManager, que é o único ponto que
            //  TODA entrada no menu passa.)
            var menu = current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (menu != null && menu.actionsAsset == null)
                menu.AssignDefaultActions();

            EnsureMenuNavigable();
            return current;
        }

        GameObject go = new GameObject("EventSystem");
        UnityEngine.EventSystems.EventSystem system = go.AddComponent<UnityEngine.EventSystems.EventSystem>();

        // O projeto usa o Input System (activeInputHandler = 1): o módulo antigo não funciona.
        if (go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        EnsureMenuNavigable();
        return system;
    }

    /// <summary>Monta e liga tudo o que a partida precisa. Idempotente.</summary>
    public void Install()
    {
        GameSettings.ApplyRuntimeSettings();
        EnsureEventSystem();
        EnsureAudio();
        EnsureAtalhoParaOMenu();

        if (difficultyCatalog == null) difficultyCatalog = DifficultyCatalogLocator.Find();
        if (playerStats == null) playerStats = Resources.Load<PlayerStats>("PlayerStats");
        if (spawnerConfig == null) spawnerConfig = Resources.Load<SpawnerConfig>("SpawnerConfig");

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // Escudo de dano do jogador (shader autoral Arena/EscudoDoTanque).
        //
        // O caminho normal é o escudo JÁ estar na cena: montado uma vez por
        // Arena > Efeitos > Montar Escudo do Tanque na Cena e salvo com o Tanque. Nesse caso
        // CriarNo encontra o existente e não cria nada. A criação em runtime fica só como rede
        // de segurança para cenas antigas — o objetivo é não depender dela.
        if (player != null && player.GetComponentInChildren<EscudoDoTanque>(true) == null)
        {
            Debug.LogWarning("[ArenaGameInitializer] O escudo do tanque não está na cena. " +
                             "Criando em runtime desta vez; para deixá-lo fixo no Tanque, rode " +
                             "Arena > Efeitos > Montar Escudo do Tanque na Cena e salve a cena.",
                             player);
        }

        EscudoDoTanque.CriarNo(player != null ? player.transform : null);

        // 1. Dependências primeiro.
        ArenaSpawner spawner = FindFirstObjectByType<ArenaSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObject = new GameObject("ArenaSpawner");
            spawner = spawnerObject.AddComponent<ArenaSpawner>();
        }

        // O HUD e o Game Over vêm montados do prefab ArenaGame (UI feita à mão).
        // Nada é criado em código: se faltar, avisamos para o prefab voltar à cena.
        if (hud == null) hud = FindFirstObjectByType<HUDController>();
        // A cena pode ter sobras de ArenaGameOverUI/HUDController sem painel configurado.
        // Só interessa o que está realmente configurado.
        if (hud == null || !hud.Configurado) hud = EscolherHud();
        if (hud == null)
            Debug.LogWarning("[ArenaGameInitializer] HUDController não encontrado. " +
                             "Coloque o prefab ArenaGame (Assets/Prefabs/ArenaGame.prefab) na cena.");

        if (gameOver == null || !gameOver.Configurado) gameOver = EscolherGameOver();
        if (gameOver == null)
            Debug.LogWarning("[ArenaGameInitializer] ArenaGameOverUI não encontrado. " +
                             "Coloque o prefab ArenaGame (Assets/Prefabs/ArenaGame.prefab) na cena.");

        // 2. ArenaManager por último, já com tudo pronto.
        ArenaManager manager = FindFirstObjectByType<ArenaManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("ArenaManager");
            manager = managerObject.AddComponent<ArenaManager>();
        }

        manager.Configure(difficultyCatalog, playerStats, spawner, hud, gameOver);

        EnsureProjectilePool(player);

        if (player != null)
        {
            Vida life = player.GetComponentInParent<Vida>();
            TankMovement movement = player.GetComponent<TankMovement>();
            TankPlayerInput input = player.GetComponent<TankPlayerInput>();
            if (hud != null) hud.Bind(life, movement, input, manager);
        }

    }

    /// <summary>HUDController que tem o painel das barras configurado, ou null.</summary>
    private static HUDController EscolherHud()
    {
        foreach (HUDController candidato in FindObjectsByType<HUDController>(FindObjectsSortMode.None))
        {
            if (candidato != null && candidato.Configurado) return candidato;
        }

        return null;
    }

    /// <summary>ArenaGameOverUI que tem painel configurado, ou null.</summary>
    private static ArenaGameOverUI EscolherGameOver()
    {
        foreach (ArenaGameOverUI candidato in FindObjectsByType<ArenaGameOverUI>(FindObjectsSortMode.None))
        {
            if (candidato != null && candidato.Configurado) return candidato;
        }

        return null;
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
