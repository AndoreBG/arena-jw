using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Painel de escolha de dificuldade: três botões e um texto de detalhes que muda ao passar o
/// mouse (ou ao selecionar por teclado/controle). O botão Cancelar fecha o painel.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Menu/Difficulty Panel")]
public sealed class DifficultyPanel : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private MenuManager menu;
    [SerializeField] private Button facilButton;
    [SerializeField] private Button medioButton;
    [SerializeField] private Button dificilButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private DifficultyCatalog catalog;

    [Header("Textos de fallback (sem catálogo)")]
    [SerializeField] private string facilDescription = "IA fraca · 2 inimigos · spawn lento";
    [SerializeField] private string medioDescription = "IA média · 4 inimigos · spawn médio";
    [SerializeField] private string dificilDescription = "IA forte · 6 inimigos · spawn rápido";

    private void Awake()
    {
        if (menu == null) menu = GetComponentInParent<MenuManager>();
        if (catalog == null) catalog = DifficultyCatalogLocator.Find();

        Hook(facilButton, ArenaDifficulty.Facil);
        Hook(medioButton, ArenaDifficulty.Medio);
        Hook(dificilButton, ArenaDifficulty.Dificil);

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Cancel);
        }

        ShowDescription(ArenaDifficulty.Facil);
    }

    private void Hook(Button button, ArenaDifficulty difficulty)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => Choose(difficulty));

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddTrigger(trigger, EventTriggerType.PointerEnter, () => ShowDescription(difficulty));
        AddTrigger(trigger, EventTriggerType.Select, () => ShowDescription(difficulty));
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    public void ShowDescription(ArenaDifficulty difficulty)
    {
        if (descriptionText == null) return;

        DifficultyConfig config = catalog != null ? catalog.Get(difficulty) : null;
        if (config != null)
        {
            descriptionText.text = config.DisplayName + ": " + config.Description;
            return;
        }

        descriptionText.text = difficulty switch
        {
            ArenaDifficulty.Medio => medioDescription,
            ArenaDifficulty.Dificil => dificilDescription,
            _ => facilDescription
        };
    }

    public void Choose(ArenaDifficulty difficulty)
    {
        if (menu == null) menu = GetComponentInParent<MenuManager>();
        if (menu == null)
        {
            GameSettings.SaveDifficulty(difficulty);
            MenuManager.TryLoadScene(MenuManager.GameSceneName);
            return;
        }

        menu.PlayWithDifficulty((int)difficulty);
    }

    public void Cancel()
    {
        if (menu == null) menu = GetComponentInParent<MenuManager>();
        if (menu != null) menu.BackToMain();
        else gameObject.SetActive(false);
    }

    /// <summary>Ligação usada pelo Editor script.</summary>
    public void Configure(MenuManager manager, Button facil, Button medio, Button dificil, Button cancel, TMP_Text description)
    {
        menu = manager;
        facilButton = facil;
        medioButton = medio;
        dificilButton = dificil;
        cancelButton = cancel;
        descriptionText = description;
    }
}
