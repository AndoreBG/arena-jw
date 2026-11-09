using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Arena/Arena Game Over UI")]
public sealed class ArenaGameOverUI : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Canvas do painel (GameOverPanel). É ele que aparece e some.")]
    [SerializeField] private GameObject panel;

    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_Text messageText;

    [Header("Controle / Teclado Virtual")]
    [Tooltip("Abre o teclado virtual automaticamente assim que o foco do controle entra no NicknameInput.")]
    [SerializeField] private bool abrirTecladoAoEntrarNoInput = true;

    private int score;
    private ArenaDifficulty difficulty = ArenaDifficulty.Facil;
    private bool busy;

    private TecladoVirtualNickname tecladoVirtual;
    private Button botaoTentarNovamente;

    public bool Configurado => panel != null;

    private void Awake()
    {
        if (panel == null)
        {
            Debug.LogWarning("[ArenaGameOverUI] Sem painel configurado neste objeto. " +
                             "Desligando este componente (o Game Over válido é o do prefab ArenaGame).", this);
            enabled = false;
            return;
        }

        PrepararTecladoVirtual();
        panel.SetActive(false);
    }

    private void Update()
    {
        if (panel == null || !panel.activeInHierarchy || nicknameInput == null) return;

        bool usandoControle = NavegacaoDoControle.UsandoControle;
        nicknameInput.shouldActivateOnSelect = !usandoControle;

        if (usandoControle && nicknameInput.isFocused)
            nicknameInput.DeactivateInputField();
    }

    private void PrepararTecladoVirtual()
    {
        Transform janela = panel.transform.Find("Janela");
        CanvasGroup grupoJanela = null;
        if (janela != null)
        {
            grupoJanela = janela.GetComponent<CanvasGroup>();
            if (grupoJanela == null) grupoJanela = janela.gameObject.AddComponent<CanvasGroup>();
        }

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == "BotaoTentarNovamente")
            {
                botaoTentarNovamente = button;
                break;
            }
        }

        tecladoVirtual = panel.GetComponent<TecladoVirtualNickname>();
        if (tecladoVirtual == null) tecladoVirtual = panel.AddComponent<TecladoVirtualNickname>();

        tecladoVirtual.Configurar(nicknameInput, grupoJanela, botaoTentarNovamente);

        if (nicknameInput != null)
        {
            GatilhoTecladoNickname gatilho = nicknameInput.GetComponent<GatilhoTecladoNickname>();
            if (gatilho == null) gatilho = nicknameInput.gameObject.AddComponent<GatilhoTecladoNickname>();
            gatilho.Configurar(nicknameInput, tecladoVirtual, abrirTecladoAoEntrarNoInput);
        }
    }

    public void Show(int finalScore, ArenaDifficulty currentDifficulty)
    {
        score = finalScore;
        difficulty = currentDifficulty;

        if (panel == null)
        {
            Debug.LogError("[ArenaGameOverUI] Campo 'Panel' vazio: não há como abrir o Game Over.", this);
            return;
        }

        panel.SetActive(true);
        ArenaGameInitializer.EnsureEventSystem();

        if (scoreText != null) scoreText.text = "Pontuação final: " + score;

        SetButtons(true);

        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = 3;
            nicknameInput.text = string.Empty;

            if (NavegacaoDoControle.UsandoControle)
            {
                nicknameInput.shouldActivateOnSelect = false;
                nicknameInput.DeactivateInputField();

                if (tecladoVirtual != null)
                    tecladoVirtual.Abrir();
                else if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(nicknameInput.gameObject);
            }
            else
            {
                if (tecladoVirtual != null) tecladoVirtual.FecharSemMoverFoco();
                nicknameInput.shouldActivateOnSelect = true;
                nicknameInput.ActivateInputField();
            }
        }

        SetMessage(NavegacaoDoControle.UsandoControle
            ? "Use o controle para escolher 3 caracteres (A–Z, 0–9)."
            : "Digite 3 caracteres: letras A–Z ou números 0–9.");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Show(int finalScore) => Show(finalScore, ArenaManager.Instance != null
        ? ArenaManager.Instance.CurrentDifficulty
        : GameSettings.Difficulty);

    public void SubmitAndStartNew()
    {
        if (!TrySubmit()) return;

        if (ArenaManager.Instance != null) ArenaManager.Instance.StartNewGame();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void SubmitAndReturnToMenu()
    {
        if (!TrySubmit()) return;

        string error;
        if (ArenaManager.Instance != null && !ArenaManager.Instance.TryReturnToMenu(out error))
            SetMessage(error);
    }

    private bool TrySubmit()
    {
        ArenaManager arena = ArenaManager.Instance;
        if (busy || arena == null) return false;

        busy = true;
        SetButtons(false);

        string result;
        RankingService.SubmitStatus status = RankingService.Submit(
            nicknameInput != null ? nicknameInput.text : string.Empty, score, difficulty, out result);

        if (status == RankingService.SubmitStatus.InvalidNickname || status == RankingService.SubmitStatus.Error)
        {
            SetMessage(result);
            busy = false;
            SetButtons(true);
            return false;
        }

        SetMessage(status == RankingService.SubmitStatus.Unchanged
            ? "Partida registrada; o ranking não mudou (recorde anterior maior)."
            : "Pontuação salva no ranking.");

        busy = false;
        SetButtons(true);
        return true;
    }

    private void SetMessage(string value)
    {
        if (messageText != null) messageText.text = value;
    }

    private void SetButtons(bool state)
    {
        Transform raiz = panel != null ? panel.transform : transform;

        foreach (Button button in raiz.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == "BotaoTentarNovamente" ||
                button.gameObject.name == "BotaoMenuPrincipal")
                button.interactable = state;
        }
    }
}

/// <summary>
/// Intercepta foco, Submit (botão A/Cruz) e clique de mouse no NicknameInput.
/// </summary>
[DisallowMultipleComponent]
public sealed class GatilhoTecladoNickname : MonoBehaviour, ISelectHandler, ISubmitHandler, IPointerClickHandler
{
    private TMP_InputField input;
    private TecladoVirtualNickname teclado;
    private bool abrirAoSelecionar;

    public void Configurar(TMP_InputField campo, TecladoVirtualNickname tecladoVirtual, bool abrirNoSelect)
    {
        input = campo;
        teclado = tecladoVirtual;
        abrirAoSelecionar = abrirNoSelect;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (input == null || !NavegacaoDoControle.UsandoControle) return;

        input.shouldActivateOnSelect = false;
        input.DeactivateInputField();

        if (!abrirAoSelecionar || teclado == null || teclado.EstaAberto) return;

        // Evita reabrir no mesmo instante em que o jogador fechou com B.
        if (Time.unscaledTime - teclado.FechadoEm < 0.2f) return;

        teclado.Abrir();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (input == null || teclado == null || !NavegacaoDoControle.UsandoControle) return;
        if (teclado.EstaAberto) return;

        teclado.Abrir();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (input == null) return;

        if (teclado != null && teclado.EstaAberto)
            teclado.FecharSemMoverFoco();

        input.shouldActivateOnSelect = true;
        input.ActivateInputField();
    }
}