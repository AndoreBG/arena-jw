using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Painel de Game Over: título, pontuação final, apelido (3 caracteres) e dois botões.
///
/// A UI vive no prefab <c>Assets/Prefabs/ArenaGame.prefab</c> (GameObject
/// <c>GameOverUI > GameOverPanel</c>) — nada é criado em código. Este script só abre/fecha
/// o painel, preenche os textos e trata o clique dos botões.
/// </summary>
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

    private int score;
    private ArenaDifficulty difficulty = ArenaDifficulty.Facil;
    private bool busy;

    /// <summary>
    /// True quando este componente realmente tem um painel para controlar.
    /// Um ArenaGameOverUI sem painel (sobra na cena) fica desligado e não atrapalha.
    /// </summary>
    public bool Configurado => panel != null;

    private void Awake()
    {
        // NUNCA usar 'panel = gameObject' aqui: este componente pode estar no mesmo objeto
        // do ArenaManager, e desativar o próprio GameObject desligaria a partida inteira.
        if (panel == null)
        {
            Debug.LogWarning("[ArenaGameOverUI] Sem painel configurado neste objeto. " +
                             "Desligando este componente (o Game Over válido é o do prefab ArenaGame).", this);
            enabled = false;
            return;
        }

        panel.SetActive(false);
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

    /// <summary>Botão "Tentar Novamente".</summary>
    public void SubmitAndStartNew()
    {
        if (!TrySubmit()) return;

        if (ArenaManager.Instance != null) ArenaManager.Instance.StartNewGame();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Botão "Menu Principal".</summary>
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
        // O painel pode estar em outro Canvas, então procuramos a partir dele (e não do
        // GameObject deste componente, que pode ser o próprio ArenaManager).
        Transform raiz = panel != null ? panel.transform : transform;

        foreach (Button button in raiz.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == "BotaoTentarNovamente" ||
                button.gameObject.name == "BotaoMenuPrincipal")
                button.interactable = state;
        }
    }
}
