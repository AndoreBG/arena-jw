using UnityEngine;

/// <summary>
/// Configurações do jogo em PlayerPrefs. Um único lugar lê e escreve, para o painel de
/// configurações e o bootstrap do jogo nunca divergirem nas chaves.
/// </summary>
public static class GameSettings
{
    public const string MasterVolumeKey = "Settings_MasterVolume";
    public const string SfxVolumeKey = "Settings_SfxVolume";
    public const string MusicVolumeKey = "Settings_MusicVolume";
    public const string ResolutionIndexKey = "Settings_ResolutionIndex";
    public const string DisplayModeKey = "Settings_DisplayMode";
    public const string DifficultyKey = "Arena_Difficulty";

    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        set => PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
    }

    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f));
        set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
    }

    public static int ResolutionIndex
    {
        get => Mathf.Max(0, PlayerPrefs.GetInt(ResolutionIndexKey, 0));
        set => PlayerPrefs.SetInt(ResolutionIndexKey, Mathf.Max(0, value));
    }

    /// <summary>0 = Janela, 1 = Tela cheia (janela), 2 = Tela cheia exclusiva.</summary>
    public static int DisplayMode
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(DisplayModeKey, 1), 0, 2);
        set => PlayerPrefs.SetInt(DisplayModeKey, Mathf.Clamp(value, 0, 2));
    }

    public static ArenaDifficulty Difficulty
    {
        get => (ArenaDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)ArenaDifficulty.Facil), 0, 2);
        set => PlayerPrefs.SetInt(DifficultyKey, (int)value);
    }

    /// <summary>Grava tudo de uma vez e aplica (resolução e volumes).</summary>
    public static void Save(float master, float sfx, float music, int resolutionIndex, int displayMode)
    {
        MasterVolume = master;
        SfxVolume = sfx;
        MusicVolume = music;
        ResolutionIndex = resolutionIndex;
        DisplayMode = displayMode;
        PlayerPrefs.Save();
        ApplyRuntimeSettings();
    }

    public static void SaveDifficulty(ArenaDifficulty difficulty)
    {
        Difficulty = difficulty;
        PlayerPrefs.Save();
    }

    /// <summary>Aplica o que faz efeito imediato: volumes e resolução/modo de exibição.</summary>
    public static void ApplyRuntimeSettings()
    {
        AudioSettings.ApplySaved();
        ApplyDisplay();
    }

    public static void ApplyDisplay()
    {
        if (Application.isEditor) return; // Não mexer na janela do Editor.

        ResolutionOption[] options = Resources.LoadAll<ResolutionOption>("Resolutions");
        FullScreenMode mode = DisplayMode switch
        {
            0 => FullScreenMode.Windowed,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.ExclusiveFullScreen
        };

        if (options != null && options.Length > 0)
        {
            int index = Mathf.Clamp(ResolutionIndex, 0, options.Length - 1);
            ResolutionOption option = options[index];
            Screen.SetResolution(option.Width, option.Height, mode);
        }
        else
        {
            Screen.fullScreenMode = mode;
        }
    }
}

/// <summary>
/// Volumes do jogo.
///
/// Se existir um AudioMixer em <c>Resources/ArenaMixer</c> com os parâmetros expostos
/// <c>MasterVolume</c>, <c>MusicVolume</c> e <c>SfxVolume</c>, os volumes são aplicados nele
/// (escala logarítmica, como o mixer espera). Sem mixer, o volume geral cai no
/// <c>AudioListener.volume</c> e os volumes de efeito/música ficam disponíveis para quem for tocar
/// som. Passo a passo para criar o mixer: <c>Docs/AudioMixer.md</c>.
/// </summary>
public static class AudioSettings
{
    public const string MixerResourcesPath = "ArenaMixer";
    public const string MasterParameter = "MasterVolume";
    public const string MusicParameter = "MusicVolume";
    public const string SfxParameter = "SfxVolume";

    private static UnityEngine.Audio.AudioMixer mixer;
    private static bool mixerSearched;

    public static float Master => GameSettings.MasterVolume;
    public static float Sfx => GameSettings.SfxVolume;
    public static float Music => GameSettings.MusicVolume;

    /// <summary>Volume final de um efeito: master × sfx (0..1).</summary>
    public static float EffectiveSfx => Master * Sfx;

    /// <summary>Volume final da música: master × música (0..1).</summary>
    public static float EffectiveMusic => Master * Music;

    /// <summary>Mixer em uso, ou null quando o projeto ainda não tem um.</summary>
    public static UnityEngine.Audio.AudioMixer Mixer
    {
        get
        {
            if (!mixerSearched)
            {
                mixerSearched = true;
                mixer = Resources.Load<UnityEngine.Audio.AudioMixer>(MixerResourcesPath);
            }
            return mixer;
        }
    }

    /// <summary>Aplica os volumes salvos. Idempotente; chamado ao iniciar e ao clicar em Aplicar.</summary>
    public static void ApplySaved()
    {
        UnityEngine.Audio.AudioMixer target = Mixer;

        if (target != null)
        {
            SetMixerVolume(target, MasterParameter, Master);
            SetMixerVolume(target, MusicParameter, Music);
            SetMixerVolume(target, SfxParameter, Sfx);
        }

        // Espelho para quem toca áudio sem mixer: o master também vale no listener.
        AudioListener.volume = Master;
    }

    /// <summary>Converte 0..1 para decibéis (−80 dB = silêncio) e grava no parâmetro exposto.</summary>
    public static bool SetMixerVolume(UnityEngine.Audio.AudioMixer target, string parameter, float linear)
    {
        if (target == null || string.IsNullOrEmpty(parameter)) return false;

        float clamped = Mathf.Clamp(linear, 0.0001f, 1f);
        float decibels = Mathf.Log10(clamped) * 20f;
        return target.SetFloat(parameter, decibels);
    }
}
