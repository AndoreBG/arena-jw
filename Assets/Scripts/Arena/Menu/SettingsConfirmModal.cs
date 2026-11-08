using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Modal de confirmação do painel de configurações. Abre quando o jogador tenta voltar com
/// mudanças não aplicadas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Menu/Settings Confirm Modal")]
public sealed class SettingsConfirmModal : MonoBehaviour
{
    public const string ModalName = "SettingsConfirmModal";

    public const string DefaultMessage =
        "Se você sair sem aplicar, as mudanças não serão salvas. Deseja realmente sair?";

    [Header("Referências")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action onConfirm;
    private bool built;
    private bool showing;

    private void Awake()
    {
        if (modalRoot == null) BuildHierarchy();

        // Não se desativa se o Awake está rodando por causa do SetActive(true) do Show — isso
        // acontece quando o objeto nasce inativo na cena e seria fechado no mesmo instante em
        // que fosse aberto.
        if (!showing && modalRoot != null) modalRoot.SetActive(false);
    }

    public void Show(Action confirmed)
    {
        if (modalRoot == null) BuildHierarchy();

        showing = true;
        onConfirm = confirmed;
        if (messageText != null) messageText.text = DefaultMessage;
        if (modalRoot != null) modalRoot.SetActive(true);

        // Seleciona "Não" para o jogador precisar de uma ação explícita para descartar.
        if (noButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }

    public void Confirm()
    {
        Action callback = onConfirm;
        onConfirm = null;
        showing = false;
        Hide();
        callback?.Invoke();
    }

    public void Dismiss()
    {
        onConfirm = null;
        showing = false;
        Hide();
    }

    private void Hide()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
    }

    /// <summary>Monta o modal em código (Editor script ou fallback).</summary>
    public void BuildHierarchy()
    {
        if (built) return;

        GameObject canvasObject = modalRoot;

        if (canvasObject != null && canvasObject.GetComponent<Canvas>() == null)
        {
            // O modal pode ser um painel comum dentro de um Canvas que já existe (é assim na
            // cena montada). Nesse caso o canvasObject continua valendo: os filhos entram nele e
            // quem desenha é o Canvas do pai. Só descartamos o objeto se ele estiver solto,
            // sem nenhum Canvas acima — aí não haveria como desenhar nada.
            if (canvasObject != gameObject && canvasObject.GetComponentInParent<Canvas>() == null)
            {
                if (Application.isPlaying) Destroy(canvasObject);
                else DestroyImmediate(canvasObject);
                canvasObject = null;
            }
        }

        if (canvasObject == null)
        {
            canvasObject = new GameObject(ModalName, typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
        }

        built = true;
        modalRoot = canvasObject;

        for (int index = canvasObject.transform.childCount - 1; index >= 0; index--)
        {
            Transform child = canvasObject.transform.GetChild(index);
            if (child == null) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        // Se já existe um Canvas acima (o modal é um painel da cena), não cria outro: Canvas
        // aninhado duplicaria o escalonamento e o desenho.
        bool insideCanvas = canvasObject.GetComponentInParent<Canvas>() != null;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null && !insideCanvas) canvas = canvasObject.AddComponent<Canvas>();

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
                canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject dim = new GameObject("Fundo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dim.transform.SetParent(canvasObject.transform, false);
        RectTransform dimRect = (RectTransform)dim.transform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        GameObject window = new GameObject("Janela", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        window.transform.SetParent(canvasObject.transform, false);
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(860f, 340f);
        window.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 1f);

        TMP_FontAsset font = null; // Fonte padrão do projeto (TMP Settings).

        messageText = CreateText(window.transform, font, DefaultMessage, 26f);
        RectTransform messageRect = (RectTransform)messageText.transform;
        messageRect.anchorMin = new Vector2(0.5f, 0.5f);
        messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.sizeDelta = new Vector2(780f, 160f);
        messageRect.anchoredPosition = new Vector2(0f, 60f);

        yesButton = CreateButton(window.transform, font, "Sim, voltar", new Vector2(-190f, -100f));
        yesButton.onClick.AddListener(Confirm);

        noButton = CreateButton(window.transform, font, "Não, permanecer", new Vector2(190f, -100f));
        noButton.onClick.AddListener(Dismiss);
    }

    private static TMP_Text CreateText(Transform parent, TMP_FontAsset font, string value, float size)
    {
        GameObject textObject = new GameObject("Mensagem", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, TMP_FontAsset font, string label, Vector2 position)
    {
        GameObject buttonObject = new GameObject("Botao_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, 68f);
        rect.anchoredPosition = position;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.45f, 0.85f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = label;
        text.fontSize = 26f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        return button;
    }

    /// <summary>Ligação usada pelo Editor script.</summary>
    public void Configure(GameObject root, TMP_Text message, Button yes, Button no)
    {
        modalRoot = root;
        messageText = message;
        yesButton = yes;
        noButton = no;
    }
}
