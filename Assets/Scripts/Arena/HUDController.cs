using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD da partida: barra de vida, barra de aceleração, pontuação e o nome da dificuldade.
/// Os retículos (câmera e cano) continuam sendo do <see cref="TankAimReticleHUD"/>; aqui só
/// escondemos/mostramos eles junto com o resto do HUD.
///
/// A hierarquia é criada por <see cref="BuildHierarchy"/> (chamado pelo Editor script) e usada
/// em runtime. Se a cena não tiver a hierarquia, o componente cria uma no primeiro Awake.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/HUD Controller")]
public sealed class HUDController : MonoBehaviour
{
    public const string RootName = "HUD_Partida";

    [Header("Referências")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image accelerationFill;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private GameObject[] reticles;

    [Header("Cores")]
    [SerializeField] private Color healthyColor = new Color(0.35f, 0.9f, 0.4f);
    [SerializeField] private Color hurtColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color accelerationColor = new Color(0.35f, 0.75f, 1f);

    private Vida life;
    private TankMovement movement;
    private TankPlayerInput input;
    private ArenaManager manager;
    private bool built;

    private void Awake()
    {
        if (healthFill == null || accelerationFill == null) BuildHierarchy();
    }

    /// <summary>Liga o HUD aos dados da partida.</summary>
    public void Bind(Vida playerLife, TankMovement tankMovement, TankPlayerInput playerInput, ArenaManager arenaManager)
    {
        Unbind();

        life = playerLife;
        movement = tankMovement;
        input = playerInput;
        manager = arenaManager;

        if (life != null) life.AoAlterarVida += OnHealthChanged;
        if (life != null) OnHealthChanged(life.VidaAtual, life.VidaMaxima);

        if (difficultyText != null && manager != null && manager.Difficulty != null)
            difficultyText.text = manager.Difficulty.DisplayName;

        if (manager != null) RefreshScore(manager.Kills);
    }

    private void OnDestroy() => Unbind();

    private void Unbind()
    {
        if (life != null) life.AoAlterarVida -= OnHealthChanged;
        life = null;
    }

    private void Update()
    {
        if (accelerationFill == null) return;

        float speed = movement != null ? Mathf.Abs(movement.NormalizedSpeed) : 0f;
        accelerationFill.fillAmount = Mathf.Clamp01(speed);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

        if (healthFill != null)
        {
            healthFill.fillAmount = normalized;
            healthFill.color = Color.Lerp(hurtColor, healthyColor, normalized);
        }

        if (healthText != null)
            healthText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(maximum);
    }

    public void RefreshScore(int kills)
    {
        if (scoreText != null) scoreText.text = "Pontuação: " + kills;
    }

    /// <summary>Esconde o HUD (retículos inclusos) durante o Game Over.</summary>
    public void SetGameOverVisible(bool visible)
    {
        if (reticles != null)
        {
            foreach (GameObject reticle in reticles)
                if (reticle != null) reticle.SetActive(!visible);
        }

        // O painel de Game Over cobre a tela; esconder o HUD evita conflito visual.
        if (root != null) root.SetActive(!visible);
    }

    public void SetRootVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }

    /// <summary>
    /// Cria a hierarquia do HUD. Pode ser chamado pelo Editor script (para a UI ficar editável na
    /// cena) ou em runtime como fallback. É idempotente: reutiliza um Canvas válido e reconstrói
    /// só o conteúdo dele.
    /// </summary>
    public void BuildHierarchy()
    {
        // Já montado nesta instância? Não mexe em nada.
        if (built) return;

        // Resolve o objeto que vai receber o Canvas. Regra: o que está gravado em 'root' manda
        // (é a hierarquia que ficou salva no prefab/cena); sem root, o próprio GameObject serve
        // se já for um Canvas (ex.: o HUD_Mira da cena).
        GameObject canvasObject = root;
        if (canvasObject == null && GetComponent<Canvas>() != null) canvasObject = gameObject;

        if (canvasObject != null && canvasObject.GetComponent<Canvas>() == null)
        {
            // 'root' aponta para um objeto que perdeu o Canvas (ou nunca teve). Em vez de
            // estourar MissingComponentException no renderMode, descarta esse objeto e começa
            // de novo num Canvas próprio.
            Debug.LogWarning($"[HUDController] O objeto '{canvasObject.name}' não tem Canvas. " +
                             "A hierarquia do HUD será reconstruída em um Canvas novo.", this);

            if (canvasObject != gameObject)
            {
                if (Application.isPlaying) Destroy(canvasObject);
                else DestroyImmediate(canvasObject);
            }

            canvasObject = null;
        }

        if (canvasObject == null)
        {
            canvasObject = new GameObject(RootName, typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            // Canvas/CanvasScaler/GraphicRaycaster entram logo abaixo.
        }

        root = canvasObject;
        built = true;

        // Reconstrói o conteúdo (some com barras/textos antigos para não duplicar ao rodar o
        // comando duas vezes).
        for (int index = canvasObject.transform.childCount - 1; index >= 0; index--)
        {
            Transform child = canvasObject.transform.GetChild(index);
            if (child == null) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            canvasObject.AddComponent<GraphicRaycaster>();

        TMP_FontAsset font = null; // Ao nascer sem fonte, o TMP usa a padrão do projeto (TMP Settings).

        // --- Barra de vida (centro inferior) ---
        GameObject healthBackground = CreatePanel(root.transform, "BarraVida_Fundo",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(540f, 34f), new Vector2(0f, 96f),
            new Color(0f, 0f, 0f, 0.55f));

        healthFill = CreateFill(healthBackground.transform, "BarraVida", healthyColor);

        healthText = CreateText(healthBackground.transform, "VidaTexto", font, "100 / 100", 20f,
            TextAlignmentOptions.Center, Color.white);
        Stretch((RectTransform)healthText.transform);

        // --- Barra de aceleração (abaixo da vida) ---
        GameObject accelerationBackground = CreatePanel(root.transform, "Aceleracao_Fundo",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(540f, 14f), new Vector2(0f, 72f),
            new Color(0f, 0f, 0f, 0.55f));

        accelerationFill = CreateFill(accelerationBackground.transform, "Aceleracao", accelerationColor);

        // --- Pontuação (canto superior direito) ---
        scoreText = CreateText(root.transform, "Pontuacao", font, "Pontuação: 0", 34f,
            TextAlignmentOptions.TopRight, Color.white);
        RectTransform scoreRect = (RectTransform)scoreText.transform;
        scoreRect.anchorMin = new Vector2(1f, 1f);
        scoreRect.anchorMax = new Vector2(1f, 1f);
        scoreRect.pivot = new Vector2(1f, 1f);
        scoreRect.sizeDelta = new Vector2(420f, 56f);
        scoreRect.anchoredPosition = new Vector2(-40f, -30f);
        scoreText.fontStyle = FontStyles.Bold;

        // --- Dificuldade (canto superior esquerdo) ---
        difficultyText = CreateText(root.transform, "Dificuldade", font, "Fácil", 26f,
            TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.85f));
        RectTransform difficultyRect = (RectTransform)difficultyText.transform;
        difficultyRect.anchorMin = new Vector2(0f, 1f);
        difficultyRect.anchorMax = new Vector2(0f, 1f);
        difficultyRect.pivot = new Vector2(0f, 1f);
        difficultyRect.sizeDelta = new Vector2(420f, 48f);
        difficultyRect.anchoredPosition = new Vector2(40f, -30f);

        built = true;
    }

    /// <summary>Registra os retículos existentes (filhos do HUD_Mira) para poder escondê-los.</summary>
    public void SetReticles(params GameObject[] values) => reticles = values;

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 size, Vector2 position, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return panel;
    }

    private static Image CreateFill(Transform parent, string name, Color color)
    {
        GameObject fill = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)fill.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);

        Image image = fill.GetComponent<Image>();
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
        image.raycastTarget = false;

        return image;
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
