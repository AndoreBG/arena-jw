using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Navegação da cena de menu: Menu Principal ↔ painel de dificuldade ↔ painel de configurações.
/// Só existe um painel visível por vez. O componente vive em um GameObject que nunca é
/// desativado (os painéis são filhos dele), para os botões continuarem respondendo.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Menu/Menu Manager")]
public sealed class MenuManager : MonoBehaviour
{
    public const string GameSceneName = "playground";
    public const string MenuSceneName = "MenuPrincipal";

    [Header("Painéis")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject difficultyPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Botões do menu principal")]
    [Tooltip("Abre o painel de dificuldade.")]
    [SerializeField] private Button playButton;
    [Tooltip("Abre o painel de configurações.")]
    [SerializeField] private Button settingsButton;
    [Tooltip("Fecha o jogo.")]
    [SerializeField] private Button quitButton;

    [Header("Jogo")]
    [Tooltip("Nome da cena do jogo no Build Settings.")]
    [SerializeField] private string gameSceneName = GameSceneName;
    [Tooltip("Cena opcional carregada antes do Playground (ex.: tutorial). Vazio = nenhuma.")]
    [SerializeField] private string introSceneName = "";
    [Tooltip("Texto exibido quando a cena do jogo não está no Build Settings.")]
    [SerializeField] private TMPro.TMP_Text messageText;

    public bool IsMainOpen => mainPanel != null && mainPanel.activeSelf;
    public bool IsDifficultyOpen => difficultyPanel != null && difficultyPanel.activeSelf;
    public bool IsSettingsOpen => settingsPanel != null && settingsPanel.activeSelf;

    private void Awake()
    {
        GameSettings.ApplyRuntimeSettings();
        ArenaGameInitializer.EnsureEventSystem();

        // Os botões são ligados aqui, e não no Editor script: um AddListener feito em tempo de
        // Editor não fica gravado na cena, então os botões nasciam sem nenhuma ação.
        Wire(playButton, ShowDifficulty);
        Wire(settingsButton, ShowSettings);
        Wire(quitButton, QuitGame);

        ShowMain();
        if (messageText != null) messageText.gameObject.SetActive(false);

        // O menu abre sem nada selecionado, e o Unity só navega no D-pad/stick quando há
        // seleção. Faz aqui (e não num componente solto) porque este Awake é o ponto por
        // onde toda entrada no menu passa.
        ArenaGameInitializer.EnsureMenuNavigable();
        ArenaGameInitializer.ConectarNavegacaoDoControle();
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void ShowMain() => SetActivePanel(mainPanel);

    public void ShowDifficulty() => SetActivePanel(difficultyPanel);
    public void ShowSettings() => SetActivePanel(settingsPanel);

    /// <summary>Fecha o painel atual e volta para o menu principal.</summary>
    public void BackToMain() => ShowMain();

    /// <summary>Grava a dificuldade escolhida e carrega o Playground.</summary>
    public void PlayWithDifficulty(int difficulty)
    {
        GameSettings.SaveDifficulty((ArenaDifficulty)Mathf.Clamp(difficulty, 0, 2));

        if (!string.IsNullOrWhiteSpace(introSceneName) && TryLoadScene(introSceneName)) return;

        if (!TryLoadScene(gameSceneName))
        {
            string message = "A cena '" + gameSceneName + "' não está no Build Settings. " +
                             "Adicione File > Build Settings.";
            ShowMessage(message);
            Debug.LogError("[MenuManager] " + message);
        }
    }

    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.text = message;
        }
    }

    public void PlayOnFacil() => PlayWithDifficulty((int)ArenaDifficulty.Facil);
    public void PlayOnMedio() => PlayWithDifficulty((int)ArenaDifficulty.Medio);
    public void PlayOnDificil() => PlayWithDifficulty((int)ArenaDifficulty.Dificil);

    /// <summary>Sai do jogo (no Editor, para o Play Mode).</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Carrega uma cena do Build Settings pelo nome. Aceita variações de caixa.</summary>
    public static bool TryLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(i);
                return true;
            }
        }

        return false;
    }

    private void SetActivePanel(GameObject panel)
    {
        SetActiveIfPresent(settingsPanel, panel);
        SetActiveIfPresent(difficultyPanel, panel);
        SetActiveIfPresent(mainPanel, panel);

        if (panel != null && !panel.activeSelf) panel.SetActive(true);

        // O botão que estava selecionado acabou de ser desligado junto com o painel antigo.
        // Sem reeleger, a navegação morre e o jogador precisa do mouse para voltar.
        StartCoroutine(SelecionarPrimeiroNoFimDoFrame());
    }

    /// <summary>
    /// Espera um frame: o layout do painel recém-aberto só está calculado no fim do frame,
    /// e antes disso a posição na tela é a do painel antigo.
    /// </summary>
    private IEnumerator SelecionarPrimeiroNoFimDoFrame()
    {
        yield return null;

        EventSystem sistema = EventSystem.current;
        if (sistema == null) yield break;   // yield break: return vazio não compila em iterador

        // A seleção anterior morreu junto com o painel; limpa antes de escolher.
        if (sistema.currentSelectedGameObject == null ||
            !sistema.currentSelectedGameObject.activeInHierarchy)
        {
            sistema.SetSelectedGameObject(null);
            ArenaGameInitializer.EnsureMenuNavigable();
        }
    }

    private static void SetActiveIfPresent(GameObject candidate, GameObject active)
    {
        if (candidate != null && candidate != active && candidate.activeSelf) candidate.SetActive(false);
    }

    /// <summary>Lista de painéis, útil para o Editor script ligar as referências.</summary>
    public void Configure(GameObject main, GameObject difficulty, GameObject settings)
    {
        mainPanel = main;
        difficultyPanel = difficulty;
        settingsPanel = settings;
    }

    /// <summary>Versão com os botões do menu principal (usada pelo Editor script).</summary>
    public void Configure(GameObject main, GameObject difficulty, GameObject settings,
        Button play, Button configuracoes, Button sair)
    {
        Configure(main, difficulty, settings);
        playButton = play;
        settingsButton = configuracoes;
        quitButton = sair;
    }
}
