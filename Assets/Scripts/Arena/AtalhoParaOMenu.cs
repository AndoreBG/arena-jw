using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Atalho de saída da partida: segurar <b>Esc</b> por alguns segundos volta para o menu
/// <b>sem gravar a pontuação</b>.
///
/// Por que segurar em vez de apertar: Esc é fácil de esbarrar sem querer, e sair da partida
/// no meio é irreversível (não tem pausa). Segurar 3 segundos deixa claro que foi intencional.
///
/// A pontuação não é gravada porque este script <b>nunca chama</b> o
/// <see cref="RankingService"/>. Quem grava é o <see cref="ArenaGameOverUI"/>, e só quando o
/// jogador clica em um dos botões do Game Over. Aqui a gente só troca de cena.
///
/// Só funciona durante a partida: com o Game Over aberto, os botões continuam mandando.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Atalho Para o Menu")]
public sealed class AtalhoParaOMenu : MonoBehaviour
{
    public const string ResourceFolder = "UI";

    /// <summary>Cena para onde este atalho leva.</summary>
    private const string NomeDaCenaDeMenu = "menu_principal";

    [Header("Atalho")]
    [Tooltip("Tecla que precisa ficar pressionada para voltar ao menu.")]
    [SerializeField] private Key tecla = Key.Escape;

    [Tooltip("Quantos segundos a tecla precisa ficar pressionada.")]
    [SerializeField, Min(0.5f)] private float segundosSegurando = 3f;

    [Tooltip("Funciona também com o Game Over aberto? (o normal é não)")]
    [SerializeField] private bool funcionarNoGameOver = false;

    [Header("Aviso na tela")]
    [Tooltip("Mostra o aviso no centro da tela enquanto a tecla está pressionada.")]
    [SerializeField] private bool mostrarAviso = true;

    [Tooltip("Texto do aviso.")]
    [SerializeField] private string textoDoAviso = "Voltando ao menu...";

    [Tooltip("Solte a tecla antes do fim e o aviso some, sem voltar ao menu.")]
    [SerializeField] private string textoParaSoltarAntes = "Solte Esc para cancelar";

    private float tempoSegurando;
    private bool voltando;

    private Canvas canvasDoAviso;
    private CanvasGroup grupoDoAviso;
    private Image barraDeProgresso;
    private TMP_Text rotulo;

    /// <summary>0 a 1: quanto do caminho até voltar ao menu já foi andado.</summary>
    public float Progresso => segundosSegurando <= 0f ? 0f : Mathf.Clamp01(tempoSegurando / segundosSegurando);

    private void Awake()
    {
        // O aviso não pode atrapalhar a UI existente: fica num canvas próprio, na frente.
        if (mostrarAviso) CriarAviso();
        MostrarAviso(false);
    }

    private void Update()
    {
        if (voltando) return;

        // Com o Game Over aberto, quem manda são os botões (a não ser que peçam o contrário).
        if (!funcionarNoGameOver && !ArenaManager.IsPlaying)
        {
            Cancelar();
            return;
        }

        Keyboard teclado = Keyboard.current;
        if (teclado == null)
        {
            Cancelar();
            return;
        }

        bool pressionada = teclado[tecla].isPressed;

        if (!pressionada)
        {
            Cancelar();
            return;
        }

        tempoSegurando += Time.unscaledDeltaTime;   // imune a Time.timeScale

        MostrarAviso(true);
        AtualizarAviso();

        if (tempoSegurando >= segundosSegurando) VoltarAoMenu();
    }

    private void Cancelar()
    {
        if (tempoSegurando <= 0f) return;

        tempoSegurando = 0f;
        MostrarAviso(false);
        AtualizarAviso();
    }

    private void VoltarAoMenu()
    {
        voltando = true;
        tempoSegurando = segundosSegurando;
        AtualizarAviso();

        // IMPORTANTE: nada de RankingService.Submit aqui. A pontuação desta partida é
        // descartada de propósito.

        if (ArenaManager.Instance != null && ArenaManager.Instance.TryReturnToMenu(out string erro))
            return;

        // Sem ArenaManager (ou se ele não conseguiu achar a cena): tenta pelo nome.
        if (Application.CanStreamedLevelBeLoaded(NomeDaCenaDeMenu))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(NomeDaCenaDeMenu);
            return;
        }

        // Não deu para voltar: não deixa o jogador travado com o aviso na tela.
        Debug.LogWarning("[AtalhoParaOMenu] Não foi possível voltar ao menu: " +
                         "adicione 'menu_principal' em File > Build Settings.", this);
        voltando = false;
        tempoSegurando = 0f;
        MostrarAviso(false);
    }

    // ------------------------------------------------------------------ aviso na tela

    private void CriarAviso()
    {
        GameObject raiz = new GameObject("AvisoVoltarAoMenu");
        raiz.transform.SetParent(transform, false);

        canvasDoAviso = raiz.AddComponent<Canvas>();
        canvasDoAviso.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasDoAviso.sortingOrder = 500;   // acima de tudo (o HUD usa 5)

        raiz.AddComponent<CanvasScaler>();
        grupoDoAviso = raiz.AddComponent<CanvasGroup>();
        grupoDoAviso.blocksRaycasts = false;
        grupoDoAviso.interactable = false;

        // Fundo escuro atrás do texto
        GameObject fundo = new GameObject("Fundo");
        fundo.transform.SetParent(raiz.transform, false);

        RectTransform rtFundo = fundo.AddComponent<RectTransform>();
        rtFundo.anchorMin = new Vector2(0.5f, 0.5f);
        rtFundo.anchorMax = new Vector2(0.5f, 0.5f);
        rtFundo.pivot = new Vector2(0.5f, 0.5f);
        rtFundo.anchoredPosition = Vector2.zero;
        rtFundo.sizeDelta = new Vector2(640f, 150f);

        Image fundoImagem = fundo.AddComponent<Image>();
        fundoImagem.color = new Color(0f, 0f, 0f, 0.72f);
        fundoImagem.raycastTarget = false;

        // Texto
        GameObject texto = new GameObject("Texto");
        texto.transform.SetParent(fundo.transform, false);

        RectTransform rtTexto = texto.AddComponent<RectTransform>();
        rtTexto.anchorMin = new Vector2(0f, 1f);
        rtTexto.anchorMax = new Vector2(1f, 1f);
        rtTexto.pivot = new Vector2(0.5f, 1f);
        rtTexto.anchoredPosition = new Vector2(0f, -18f);
        rtTexto.sizeDelta = new Vector2(-40f, 48f);

        rotulo = texto.AddComponent<TextMeshProUGUI>();
        rotulo.text = textoDoAviso;
        rotulo.fontSize = 32f;
        rotulo.alignment = TextAlignmentOptions.Center;
        rotulo.color = Color.white;
        rotulo.raycastTarget = false;

        // Barra de progresso (mesmo desenho da barra de vida: sprite branco + fillAmount)
        GameObject barraFundo = new GameObject("BarraFundo");
        barraFundo.transform.SetParent(fundo.transform, false);

        RectTransform rtBarraFundo = barraFundo.AddComponent<RectTransform>();
        rtBarraFundo.anchorMin = new Vector2(0f, 0f);
        rtBarraFundo.anchorMax = new Vector2(1f, 0f);
        rtBarraFundo.pivot = new Vector2(0.5f, 0f);
        rtBarraFundo.anchoredPosition = new Vector2(0f, 26f);
        rtBarraFundo.sizeDelta = new Vector2(-80f, 20f);

        Image imagemFundo = barraFundo.AddComponent<Image>();
        imagemFundo.color = new Color(1f, 1f, 1f, 0.18f);
        imagemFundo.raycastTarget = false;

        GameObject barraPreenchida = new GameObject("BarraPreenchida");
        barraPreenchida.transform.SetParent(barraFundo.transform, false);

        RectTransform rtBarra = barraPreenchida.AddComponent<RectTransform>();
        rtBarra.anchorMin = Vector2.zero;
        rtBarra.anchorMax = Vector2.one;
        rtBarra.pivot = new Vector2(0.5f, 0.5f);
        rtBarra.anchoredPosition = Vector2.zero;
        rtBarra.sizeDelta = Vector2.zero;

        barraDeProgresso = barraPreenchida.AddComponent<Image>();
        barraDeProgresso.color = new Color(1f, 0.72f, 0.2f, 1f);   // âmbar, para não confundir com vida
        barraDeProgresso.raycastTarget = false;

        // Sem sprite, o Image ignora o "Filled" e o fillAmount não faz nada (foi o bug da
        // barra de vida). Este é o sprite branco embutido da UI do Unity.
        barraDeProgresso.sprite = SpriteDeUI();
        barraDeProgresso.type = Image.Type.Filled;
        barraDeProgresso.fillMethod = Image.FillMethod.Horizontal;
        barraDeProgresso.fillOrigin = (int)Image.OriginHorizontal.Left;
        barraDeProgresso.fillAmount = 0f;
    }

    /// <summary>
    /// O sprite branco embutido do Unity (UI/Skin/UISprite.psd). Em build ele vem pelo
    /// Resources, então não depende de nenhum asset do projeto.
    /// </summary>
    private static Sprite SpriteDeUI()
    {
        Sprite sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        if (sprite == null)
            sprite = Resources.Load<Sprite>(ResourceFolder + "/UISprite");

        return sprite;
    }

    private void AtualizarAviso()
    {
        if (barraDeProgresso != null)
            barraDeProgresso.fillAmount = Progresso;

        if (rotulo != null)
            rotulo.text = textoDoAviso;
    }

    private void MostrarAviso(bool visivel)
    {
        if (canvasDoAviso == null) return;

        if (grupoDoAviso != null)
        {
            grupoDoAviso.alpha = visivel ? 1f : 0f;
        }

        canvasDoAviso.enabled = visivel;
    }
}
