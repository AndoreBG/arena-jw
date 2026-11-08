using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD da partida: barra de vida, pontuação e o nome da dificuldade.
///
/// A hierarquia vive no prefab <c>Assets/Prefabs/ArenaGame.prefab</c> (GameObject
/// <c>HUD_Partida</c>) — nada é criado em código. Este script só preenche os valores e
/// liga/desliga o que precisa durante a partida.
///
/// No Game Over desligamos o <c>HUD_Partida</c> (as barras) e os retículos. O objeto
/// <c>Canva</c> da cena — que guarda os retículos — nunca é desativado: ele continua vivo,
/// só os dois retículos somem.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/HUD Controller")]
public sealed class HUDController : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Objeto que contém a barra de vida, a pontuação e a dificuldade.")]
    [SerializeField] private GameObject hud;

    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text difficultyText;

    [Tooltip("Retículos que já existem na cena (ReticuloCamera e ReticuloCanhao, filhos do Canva).")]
    [SerializeField] private GameObject[] reticles;

    [Header("Cores")]
    [SerializeField] private Color healthyColor = new Color(0.35f, 0.9f, 0.4f);
    [SerializeField] private Color hurtColor = new Color(1f, 0.25f, 0.2f);

    private Vida life;
    private TankPlayerInput input;
    private ArenaManager manager;

    /// <summary>True quando este HUD realmente tem o painel das barras configurado.</summary>
    public bool Configurado => hud != null;

    private void Awake()
    {
        // A cena pode ter os retículos soltos no Canva; se o Inspector estiver vazio, achamos.
        if (reticles == null || reticles.Length == 0) reticles = ProcurarReticulos();
    }

    /// <summary>
    /// Liga o HUD aos dados da partida. O <paramref name="tankMovement"/> continua na assinatura
    /// porque o inicializador já chama assim (o HUD não usa mais a velocidade).
    /// </summary>
    public void Bind(Vida playerLife, TankMovement tankMovement, TankPlayerInput playerInput, ArenaManager arenaManager)
    {
        Unbind();

        life = playerLife;
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

    /// <summary>
    /// Esconde o HUD e os retículos durante o Game Over.
    /// Desliga o <c>HUD_Partida</c> (as barras) e os retículos. O <c>Canva</c> da cena
    /// continua ativo — quem manda no desenho dos retículos é o <see cref="TankAimReticleHUD"/>.
    /// </summary>
    public void SetGameOverVisible(bool visible)
    {
        DefinirVisivel(hud, !visible);

        if (reticles != null)
        {
            foreach (GameObject reticle in reticles)
                DefinirVisivel(reticle, !visible);
        }

        // O TankAimReticleHUD religa o Image do retículo a cada frame enquanto joga, então
        // avisamos ele também (senão o retículo do canhão voltava por cima do Game Over).
        TankAimReticleHUD aimHud = FindFirstObjectByType<TankAimReticleHUD>();
        if (aimHud != null) aimHud.SetReticlesVisible(!visible);
    }

    /// <summary>Some com os elementos do HUD sem tocar nos retículos.</summary>
    public void SetRootVisible(bool visible) => DefinirVisivel(hud, visible);

    public void SetReticles(params GameObject[] values) => reticles = values;

    /// <summary>Liga/desliga um objeto, sem estourar se ele ainda não foi atribuído.</summary>
    private static void DefinirVisivel(GameObject alvo, bool visivel)
    {
        if (alvo == null) return;
        alvo.SetActive(visivel);
    }

    /// <summary>Acha os retículos que já existem na cena, pelo nome.</summary>
    private static GameObject[] ProcurarReticulos()
    {
        GameObject camera = GameObject.Find("ReticuloCamera");
        GameObject canhao = GameObject.Find("ReticuloCanhao");

        if (camera == null && canhao == null) return new GameObject[0];
        if (camera == null) return new[] { canhao };
        if (canhao == null) return new[] { camera };
        return new[] { camera, canhao };
    }
}
