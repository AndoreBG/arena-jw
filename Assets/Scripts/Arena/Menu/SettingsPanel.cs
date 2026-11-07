using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Painel de configurações: três volumes, resolução, modo de exibição, Aplicar e Voltar.
///
/// "Aplicar" grava tudo em PlayerPrefs e aplica na hora. "Voltar" compara os controles com o
/// que está salvo; se houver mudança não aplicada, abre o <see cref="SettingsConfirmModal"/>.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Menu/Settings Panel")]
public sealed class SettingsPanel : MonoBehaviour
{
    [Header("Navegação")]
    [SerializeField] private MenuManager menu;
    [SerializeField] private SettingsConfirmModal confirmModal;

    [Header("Controles")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text statusText;

    private readonly List<ResolutionOption> resolutions = new List<ResolutionOption>();
    private bool suppressCallbacks;

    /// <summary>Há mudanças nos controles que ainda não foram gravadas.</summary>
    public bool HasUnsavedChanges =>
        !Mathf.Approximately(masterSlider != null ? masterSlider.value : GameSettings.MasterVolume, GameSettings.MasterVolume) ||
        !Mathf.Approximately(sfxSlider != null ? sfxSlider.value : GameSettings.SfxVolume, GameSettings.SfxVolume) ||
        !Mathf.Approximately(musicSlider != null ? musicSlider.value : GameSettings.MusicVolume, GameSettings.MusicVolume) ||
        (resolutionDropdown != null && resolutionDropdown.value != GameSettings.ResolutionIndex) ||
        (displayModeDropdown != null && displayModeDropdown.value != GameSettings.DisplayMode);

    private void Awake()
    {
        if (menu == null) menu = GetComponentInParent<MenuManager>();
        LoadResolutions();

        if (applyButton != null)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(Apply);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(TryGoBack);
        }
    }

    private void OnEnable() => LoadCurrentValues();

    private void LoadResolutions()
    {
        resolutions.Clear();

        ResolutionOption[] loaded = Resources.LoadAll<ResolutionOption>("Resolutions");
        if (loaded != null && loaded.Length > 0)
        {
            resolutions.AddRange(loaded);
            resolutions.Sort((a, b) => a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height));
        }

        if (resolutionDropdown == null) return;

        suppressCallbacks = true;
        resolutionDropdown.ClearOptions();

        List<string> labels = new List<string>();
        foreach (ResolutionOption option in resolutions) labels.Add(option.Label);
        if (labels.Count == 0) labels.Add("1920 × 1080");

        resolutionDropdown.AddOptions(labels);

        List<string> displayModes = new List<string> { "Janela", "Tela cheia (janela)", "Tela cheia exclusiva" };
        if (displayModeDropdown != null)
        {
            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(displayModes);
        }

        suppressCallbacks = false;
    }

    /// <summary>Copia os valores salvos para os controles.</summary>
    public void LoadCurrentValues()
    {
        suppressCallbacks = true;

        if (masterSlider != null) masterSlider.value = GameSettings.MasterVolume;
        if (sfxSlider != null) sfxSlider.value = GameSettings.SfxVolume;
        if (musicSlider != null) musicSlider.value = GameSettings.MusicVolume;

        if (resolutionDropdown != null)
        {
            int index = Mathf.Clamp(GameSettings.ResolutionIndex, 0, Mathf.Max(0, resolutionDropdown.options.Count - 1));
            resolutionDropdown.value = index;
            resolutionDropdown.RefreshShownValue();
        }

        if (displayModeDropdown != null)
        {
            displayModeDropdown.value = Mathf.Clamp(GameSettings.DisplayMode, 0, displayModeDropdown.options.Count - 1);
            displayModeDropdown.RefreshShownValue();
        }

        suppressCallbacks = false;
        SetStatus(string.Empty);
    }

    public void Apply()
    {
        if (suppressCallbacks) return;

        float master = masterSlider != null ? masterSlider.value : GameSettings.MasterVolume;
        float sfx = sfxSlider != null ? sfxSlider.value : GameSettings.SfxVolume;
        float music = musicSlider != null ? musicSlider.value : GameSettings.MusicVolume;
        int resolution = resolutionDropdown != null ? resolutionDropdown.value : GameSettings.ResolutionIndex;
        int display = displayModeDropdown != null ? displayModeDropdown.value : GameSettings.DisplayMode;

        GameSettings.Save(master, sfx, music, resolution, display);
        SetStatus("Configurações aplicadas.");
    }

    public void TryGoBack()
    {
        if (HasUnsavedChanges && confirmModal != null)
        {
            confirmModal.Show(LeaveWithoutSaving);
            return;
        }

        LeaveWithoutSaving();
    }

    private void LeaveWithoutSaving()
    {
        SetStatus(string.Empty);
        if (menu == null) menu = GetComponentInParent<MenuManager>();
        if (menu != null) menu.BackToMain();
        else gameObject.SetActive(false);
    }

    public void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value;
    }

    /// <summary>Ligação usada pelo Editor script.</summary>
    public void Configure(MenuManager manager, SettingsConfirmModal modal, Slider master, Slider sfx, Slider music,
        TMP_Dropdown resolution, TMP_Dropdown displayMode, Button apply, Button back, TMP_Text status)
    {
        menu = manager;
        confirmModal = modal;
        masterSlider = master;
        sfxSlider = sfx;
        musicSlider = music;
        resolutionDropdown = resolution;
        displayModeDropdown = displayMode;
        applyButton = apply;
        backButton = back;
        statusText = status;
    }
}
