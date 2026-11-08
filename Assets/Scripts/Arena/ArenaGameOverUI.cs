using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Painel de Game Over: título, pontuação final, apelido (3 caracteres) e dois botões.
/// Ambos os botões salvam a pontuação na dificuldade corrente antes de navegar.
///
/// O painel abre por cima da cena com <c>Time.timeScale = 0</c>; por isso o cursor é liberado
/// e o Input System UI module continua respondendo (usa tempo não escalado).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Arena Game Over UI")]
public sealed class ArenaGameOverUI : MonoBehaviour
{
    public const string PanelName = "GameOverPanel";

    [Header("Referências")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button returnToMenuButton;

    private int score;
    private ArenaDifficulty difficulty = ArenaDifficulty.Facil;
    private bool busy;
    private bool built;

    private void Awake()
    {
        if (panel == null) BuildHierarchy();

        if (panel == null)
            Debug.LogError("[ArenaGameOverUI] Não foi possível montar o painel de Game Over. " +
                           "Rode 'Arena > Setup Main Menu + Playground'.", this);
        else
            panel.SetActive(false);
    }

    public void Show(int finalScore, ArenaDifficulty currentDifficulty)
    {
        score = finalScore;
        difficulty = currentDifficulty;

        if (panel == null) BuildHierarchy();
        if (panel != null) panel.SetActive(true);

        if (scoreText != null) scoreText.text = "Pontuação final: " + score;

        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = 3;
            nicknameInput.text = string.Empty;
            nicknameInput.ActivateInputField();
        }

        SetMessage("Digite 3 caracteres: letras A–Z ou números 0–9.");
        SetButtons(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Compatibilidade com chamadas antigas (sem dificuldade explícita).</summary>
    public void Show(int finalScore) => Show(finalScore, ArenaManager.Instance != null
        ? ArenaManager.Instance.CurrentDifficulty
        : GameSettings.Difficulty);

    public void SubmitAndStartNew()
    {
        if (!TrySubmit()) return;
        ArenaManager.Instance.StartNewGame();
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
        if (newGameButton != null) newGameButton.interactable = state;
        if (returnToMenuButton != null) returnToMenuButton.interactable = state;
    }

    /// <summary>
    /// Monta o painel inteiro em código. Usado pelo Editor script e como fallback em runtime.
    /// </summary>
    public void BuildHierarchy()
    {
        if (built) return;

        // Reaproveita o Canvas já gravado (prefab/cena). Sem isso, rodar o comando do Editor duas
        // vezes duplicaria o painel — e um 'panel' gravado apontando para um objeto destruído
        // estoura MissingComponentException no Canvas.
        GameObject canvasObject = panel;

        if (canvasObject != null && canvasObject.GetComponent<Canvas>() == null)
        {
            Debug.LogWarning($"[ArenaGameOverUI] O objeto '{canvasObject.name}' não tem Canvas. " +
                             "O painel será reconstruído em um Canvas novo.", this);

            if (canvasObject != gameObject)
            {
                if (Application.isPlaying) Destroy(canvasObject);
                else DestroyImmediate(canvasObject);
            }

            canvasObject = null;
        }

        if (canvasObject == null)
        {
            canvasObject = new GameObject(PanelName, typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
        }

        panel = canvasObject;
        built = true;

        // Limpa o conteúdo anterior (idempotência).
        for (int index = canvasObject.transform.childCount - 1; index >= 0; index--)
        {
            Transform child = canvasObject.transform.GetChild(index);
            if (child == null) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        // O Canvas fica no próprio objeto do painel; quem controla a visibilidade é o GameObject
        // dele (SetActive em Show/Awake). O Canvas não deve ser desativado sozinho.
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            canvasObject.AddComponent<GraphicRaycaster>();

        // Fundo escuro
        GameObject dim = new GameObject("Fundo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dim.transform.SetParent(canvasObject.transform, false);
        RectTransform dimRect = (RectTransform)dim.transform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        // Janela central
        GameObject window = new GameObject("Janela", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        window.transform.SetParent(canvasObject.transform, false);
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(760f, 520f);
        window.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

        // O TMP atribui a fonte padrão do projeto (TMP Settings) sozinho ao criar o componente.
        TMP_FontAsset font = null;
        float y = 190f;

        TMP_Text title = CreateText(window.transform, "Titulo", font, "Game Over", 64f,
            TextAlignmentOptions.Center, new Color(1f, 0.35f, 0.3f));
        Place((RectTransform)title.transform, new Vector2(0f, y), new Vector2(700f, 80f));

        scoreText = CreateText(window.transform, "PontuacaoFinal", font, "Pontuação final: 0", 34f,
            TextAlignmentOptions.Center, Color.white);
        Place((RectTransform)scoreText.transform, new Vector2(0f, y - 90f), new Vector2(700f, 50f));

        TMP_Text label = CreateText(window.transform, "ApelidoLabel", font, "Seu apelido (3 caracteres)", 24f,
            TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
        Place((RectTransform)label.transform, new Vector2(0f, y - 150f), new Vector2(700f, 34f));

        nicknameInput = CreateInputField(window.transform, font);
        Place((RectTransform)nicknameInput.transform, new Vector2(0f, y - 210f), new Vector2(220f, 64f));

        messageText = CreateText(window.transform, "Mensagem", font,
            "Digite 3 caracteres: letras A–Z ou números 0–9.", 20f,
            TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f));
        Place((RectTransform)messageText.transform, new Vector2(0f, y - 268f), new Vector2(700f, 40f));

        newGameButton = CreateButton(window.transform, "BotaoTentarNovamente", font, "Tentar Novamente");
        Place((RectTransform)newGameButton.transform, new Vector2(0f, y - 350f), new Vector2(340f, 64f));
        newGameButton.onClick.AddListener(SubmitAndStartNew);

        returnToMenuButton = CreateButton(window.transform, "BotaoMenuPrincipal", font, "Menu Principal");
        Place((RectTransform)returnToMenuButton.transform, new Vector2(0f, y - 430f), new Vector2(340f, 64f));
        returnToMenuButton.onClick.AddListener(SubmitAndReturnToMenu);
    }

    private static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, string value,
        float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        return text;
    }

    private static TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font)
    {
        GameObject inputObject = new GameObject("NicknameInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        inputObject.transform.SetParent(parent, false);

        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.12f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(inputObject.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = 38f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.textComponent = text;
        input.characterLimit = 3;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Alphanumeric;
        input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        input.restoreOriginalTextOnEscape = false;
        input.onValueChanged.AddListener(value =>
        {
            // Normaliza para maiúsculas já na digitação (o ranking só aceita A–Z e 0–9).
            string upper = (value ?? string.Empty).ToUpperInvariant();
            if (upper != value) input.text = upper;
        });

        return input;
    }

    private static Button CreateButton(Transform parent, string name, TMP_FontAsset font, string label)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.45f, 0.85f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText(buttonObject.transform, "Label", font, label, 28f,
            TextAlignmentOptions.Center, Color.white);
        RectTransform textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
