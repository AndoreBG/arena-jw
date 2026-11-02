using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Game Over panel. Hook the two buttons to SubmitAndStartNew / SubmitAndReturnToMenu.</summary>
public sealed class ArenaGameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button returnToMenuButton;
    private int score;
    private bool busy;
    private void Awake() { if (panel != null) panel.SetActive(false); }
    public void Show(int finalScore)
    {
        score = finalScore;
        if (panel != null) panel.SetActive(true);
        if (scoreText != null) scoreText.text = "Pontuação final: " + score;
        if (nicknameInput != null) { nicknameInput.text = string.Empty; nicknameInput.ActivateInputField(); }
        SetMessage("Digite 3 caracteres: letras A–Z ou números 0–9.");
        SetButtons(true);
    }
    public void SubmitAndStartNew()
    {
        if (!TrySubmit()) return;
        ArenaManager.Instance.StartNewGame();
    }
    public void SubmitAndReturnToMenu()
    {
        if (!TrySubmit()) return;
        string error;
        if (!ArenaManager.Instance.TryReturnToMenu(out error)) SetMessage(error);
    }
    private bool TrySubmit()
    {
        if (busy || ArenaManager.Instance == null) return false;
        busy = true; SetButtons(false);
        string result;
        RankingService.SubmitStatus status = RankingService.Submit(nicknameInput != null ? nicknameInput.text : string.Empty, score, out result);
        if (status == RankingService.SubmitStatus.InvalidNickname || status == RankingService.SubmitStatus.Error)
        { SetMessage(result); busy = false; SetButtons(true); return false; }
        SetMessage(status == RankingService.SubmitStatus.Unchanged ? "Partida registrada; a pontuação do ranking não foi alterada." : "Pontuação salva no ranking.");
        busy = false; SetButtons(true);
        return true;
    }
    private void SetMessage(string value) { if (messageText != null) messageText.text = value; }
    private void SetButtons(bool value) { if (newGameButton != null) newGameButton.interactable = value; if (returnToMenuButton != null) returnToMenuButton.interactable = value; }
}
