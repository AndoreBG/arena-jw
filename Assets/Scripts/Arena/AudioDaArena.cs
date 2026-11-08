using UnityEngine;

/// <summary>
/// Efeitos sonoros e música da arena.
///
/// Criado automaticamente pelo <see cref="ArenaGameInitializer"/>, então não é preciso montar
/// nada na cena. Todo som é opcional: campo vazio = aquele som simplesmente não toca.
///
/// Respeita os volumes de <see cref="GameSettings"/> (Master, SFX e Música), inclusive quando
/// o jogador muda os sliders com a partida em andamento.
///
/// Os clipes em si ficam nos campos abaixo. Se o projeto for aberto e o Inspector mostrar
/// "None (Audio Clip)", arraste o arquivo de <c>Assets/Sound</c> para o campo correspondente.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Audio Da Arena")]
public sealed class AudioDaArena : MonoBehaviour
{
    public const string ResourceFolder = "Audio";

    [Header("Efeitos sonoros")]
    [Tooltip("Toca a cada disparo do jogador. (tank-shot)")]
    [SerializeField] private AudioClip tiroDoJogador;

    [Tooltip("Toca a cada disparo do inimigo. (alien-shot)")]
    [SerializeField] private AudioClip tiroDoInimigo;

    [Tooltip("Toca em loop em cada inimigo, simulando a nave alienígena vagueando. (alien-ship)")]
    [SerializeField] private AudioClip naveAlienigena;

    [Header("Música")]
    [Tooltip("Música de fundo da partida. Deixe vazio para tocar sem música.")]
    [SerializeField] private AudioClip musica;

    [Header("Volumes")]
    [Tooltip("Volume dos efeitos, antes do volume SFX das configurações.")]
    [SerializeField, Range(0f, 1f)] private float volumeDosEfeitos = 1f;

    [Tooltip("Volume da música, antes do volume Música das configurações.")]
    [SerializeField, Range(0f, 1f)] private float volumeDaMusica = 0.6f;

    [Tooltip("Volume do loop da nave alienígena, antes do volume SFX. " +
             "Baixo de propósito: é um som contínuo em cada inimigo.")]
    [SerializeField, Range(0f, 1f)] private float volumeDaNave = 0.35f;

    private AudioSource fonteDaMusica;
    private AudioSource fonteDoTiroDoJogador;
    private AudioSource fonteDoTiroDoInimigo;

    /// <summary>Loops de nave que estão tocando agora (um por inimigo vivo).</summary>
    private readonly System.Collections.Generic.List<AudioSource> fontesDasNaves =
        new System.Collections.Generic.List<AudioSource>();

    private float volumeMusicaAplicado = -1f;
    private float volumeEfeitosAplicado = -1f;

    private static AudioDaArena instancia;

    /// <summary>Instância da cena (pode não existir se o jogo não foi inicializado).</summary>
    public static AudioDaArena Instancia => instancia;

    private void Awake()
    {
        instancia = this;

        // Um AudioListener só por cena. Se já existe um na câmera, não duplicamos.
        if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
            gameObject.AddComponent<AudioListener>();

        CriarFontes();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void Start()
    {
        // Os clips podem vir como referência vinda do prefab ou, na falta dela, de Resources.
        if (tiroDoJogador == null) tiroDoJogador = Resources.Load<AudioClip>(ResourceFolder + "/tank-shot");
        if (tiroDoInimigo == null) tiroDoInimigo = Resources.Load<AudioClip>(ResourceFolder + "/alien-shot");
        if (naveAlienigena == null) naveAlienigena = Resources.Load<AudioClip>(ResourceFolder + "/alien-ship");

        AplicarVolumes();
        TocarMusica();
    }

    private void Update()
    {
        // Sliders de volume mexidos durante a partida.
        float musicaAtual = GameSettings.MasterVolume * GameSettings.MusicVolume * volumeDaMusica;
        float efeitosAtual = GameSettings.MasterVolume * GameSettings.SfxVolume * volumeDosEfeitos;

        if (!Mathf.Approximately(musicaAtual, volumeMusicaAplicado) ||
            !Mathf.Approximately(efeitosAtual, volumeEfeitosAplicado))
        {
            AplicarVolumes();
        }

        AtualizarVolumesDasNaves();
    }

    /// <summary>
    /// Um único lugar atualiza o volume de todos os inimigos. Antes cada inimigo tinha o seu
    /// próprio Update só para isso, o que não escala com vários inimigos na tela.
    /// </summary>
    private void AtualizarVolumesDasNaves()
    {
        if (fontesDasNaves.Count == 0) return;

        float alvo = GameSettings.MasterVolume * GameSettings.SfxVolume * volumeDaNave;

        // De trás para frente: dá para remover entradas sem bagunçar o índice.
        for (int i = fontesDasNaves.Count - 1; i >= 0; i--)
        {
            AudioSource fonte = fontesDasNaves[i];

            if (fonte == null)                  // o inimigo foi destruído
            {
                fontesDasNaves.RemoveAt(i);
                continue;
            }

            if (!Mathf.Approximately(fonte.volume, alvo)) fonte.volume = alvo;
        }
    }

    private void CriarFontes()
    {
        fonteDaMusica = CriarFonte("Musica", loop: true, spatial: false);
        fonteDoTiroDoJogador = CriarFonte("TiroDoJogador", loop: false, spatial: false);
        fonteDoTiroDoInimigo = CriarFonte("TiroDoInimigo", loop: false, spatial: false);
    }

    private AudioSource CriarFonte(string nome, bool loop, bool spatial)
    {
        GameObject filho = new GameObject(nome);
        filho.transform.SetParent(transform, false);

        AudioSource fonte = filho.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.loop = loop;
        fonte.spatialBlend = spatial ? 1f : 0f;
        return fonte;
    }

    private void AplicarVolumes()
    {
        volumeMusicaAplicado = GameSettings.MasterVolume * GameSettings.MusicVolume * volumeDaMusica;
        volumeEfeitosAplicado = GameSettings.MasterVolume * GameSettings.SfxVolume * volumeDosEfeitos;

        if (fonteDaMusica != null) fonteDaMusica.volume = volumeMusicaAplicado;
        if (fonteDoTiroDoJogador != null) fonteDoTiroDoJogador.volume = volumeEfeitosAplicado;
        if (fonteDoTiroDoInimigo != null) fonteDoTiroDoInimigo.volume = volumeEfeitosAplicado;
    }

    private void TocarMusica()
    {
        if (musica == null || fonteDaMusica == null) return;

        fonteDaMusica.clip = musica;
        fonteDaMusica.volume = volumeMusicaAplicado;
        fonteDaMusica.Play();
    }

    // ------------------------------------------------------------------ API usada pelo jogo

    /// <summary>Toca o som do disparo do jogador (tank-shot).</summary>
    public void TocarTiroDoJogador()
    {
        Tocar(tiroDoJogador, fonteDoTiroDoJogador);
    }

    /// <summary>Toca o som do disparo do inimigo (alien-shot).</summary>
    public void TocarTiroDoInimigo()
    {
        Tocar(tiroDoInimigo, fonteDoTiroDoInimigo);
    }

    /// <summary>
    /// Cria a fonte do loop de nave alienígena para um inimigo e começa a tocar.
    /// Um som por inimigo; o <see cref="AudioSource"/> morre junto com o GameObject.
    /// </summary>
    public AudioSource TocarNaveAlienigena(GameObject dono)
    {
        if (naveAlienigena == null || dono == null) return null;

        AudioSource fonte = dono.GetComponent<AudioSource>();
        if (fonte == null)
        {
            fonte = dono.AddComponent<AudioSource>();
            fonte.playOnAwake = false;
        }

        fonte.clip = naveAlienigena;
        fonte.loop = true;
        fonte.spatialBlend = 1f;                       // 3D: dá para localizar o inimigo pelo som
        fonte.rolloffMode = AudioRolloffMode.Linear;
        fonte.minDistance = 5f;
        fonte.maxDistance = 60f;
        fonte.volume = GameSettings.MasterVolume * GameSettings.SfxVolume * volumeDaNave;

        if (!fonte.isPlaying) fonte.Play();

        // Passa a ser atualizada pelo Update deste componente.
        if (!fontesDasNaves.Contains(fonte)) fontesDasNaves.Add(fonte);

        return fonte;
    }

    /// <summary>
    /// Um inimigo morreu: tira a fonte da lista para o Update não ficar olhando para
    /// uma fonte destruída.
    /// </summary>
    public void EsquecerNave(AudioSource fonte)
    {
        if (fonte == null) return;
        fontesDasNaves.Remove(fonte);
    }

    /// <summary>
    /// Volume atual do loop de um inimigo. O Update já cuida disso sozinho; existe para
    /// consulta e para testes.
    /// </summary>
    public float VolumeDaNave => GameSettings.MasterVolume * GameSettings.SfxVolume * volumeDaNave;

    private static void Tocar(AudioClip clip, AudioSource fonte)
    {
        if (clip == null || fonte == null) return;

        // PlayOneShot em vez de Play: dois tiros no mesmo frame não cortam um ao outro.
        fonte.PlayOneShot(clip);
    }
}
