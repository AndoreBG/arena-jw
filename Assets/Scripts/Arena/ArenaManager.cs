using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum ArenaDifficulty { Facil, Medio, Dificil }
public enum ArenaRunState { Playing, GameOver }

/// <summary>Partida corrente: dificuldade, vidas, pontuação, HUD e transição idempotente de Game Over.</summary>
public sealed class ArenaManager : MonoBehaviour
{
    [SerializeField] private ArenaDifficulty difficulty = ArenaDifficulty.Facil;
    [SerializeField] private Vida playerLife;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private ArenaSpawner spawner;
    [SerializeField] private ArenaGameOverUI gameOverUI;
    [SerializeField] private string menuSceneName = "MenuPrincipal";
    private int kills;
    private ArenaRunState state = ArenaRunState.Playing;
    private bool hudErrorReported;
    public static ArenaManager Instance { get; private set; }
    public static bool IsPlaying => Instance == null || Instance.state == ArenaRunState.Playing;
    public ArenaRunState State => state;
    public int Kills => kills;
    public Vida PlayerLife => playerLife;
    public int EnemyLimit => difficulty == ArenaDifficulty.Facil ? 2 : difficulty == ArenaDifficulty.Medio ? 4 : 6;
    public string MenuSceneName => menuSceneName;

    private void Awake()
    {
        Time.timeScale = 1f;
        Instance = this;
        if (playerLife == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerLife = player.GetComponentInParent<Vida>();
        }
        if (playerLife != null) { playerLife.DestruirAoMorrer = false; playerLife.AoMorrer += OnPlayerDied; }
        else Debug.LogError("[ArenaManager] Vida do jogador não configurada nem encontrada na tag Player.", this);
        UpdateKillsHUD();
    }
    private void Start() { if (spawner != null) spawner.SetManager(this); else Debug.LogError("[ArenaManager] ArenaSpawner não está configurado.", this); }
    private void OnDestroy()
    {
        if (playerLife != null) playerLife.AoMorrer -= OnPlayerDied;
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }
    public void RegisterEnemyKill(ArenaSpawner owner)
    {
        if (state != ArenaRunState.Playing) return;
        kills++; UpdateKillsHUD();
    }
    private void UpdateKillsHUD()
    {
        if (killsText == null) { if (!hudErrorReported) { Debug.LogError("[ArenaManager] Campo TMP_Text de eliminações não foi atribuído.", this); hudErrorReported = true; } return; }
        killsText.text = "Eliminações: " + kills;
    }
    private void OnPlayerDied()
    {
        if (state != ArenaRunState.Playing) return;
        state = ArenaRunState.GameOver;
        if (spawner != null) spawner.enabled = false;
        foreach (TankPlayerInput input in FindObjectsByType<TankPlayerInput>(FindObjectsSortMode.None)) input.enabled = false;
        foreach (ArenaEnemyAI ai in FindObjectsByType<ArenaEnemyAI>(FindObjectsSortMode.None)) ai.enabled = false;
        foreach (ArenaEnemyWeapon weapon in FindObjectsByType<ArenaEnemyWeapon>(FindObjectsSortMode.None)) weapon.SetCanFire(false);
        foreach (TankProjectile shot in FindObjectsByType<TankProjectile>(FindObjectsSortMode.None)) shot.RequestDespawn();
        if (gameOverUI != null) gameOverUI.Show(kills);
        else Debug.LogError("[ArenaManager] GameOverUI não foi atribuído.", this);
        Time.timeScale = 0f;
    }
    public void StartNewGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public bool TryReturnToMenu(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(menuSceneName)) { error = "Cena de menu ainda não configurada (integração pendente)."; return false; }
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == menuSceneName)
            { Time.timeScale = 1f; SceneManager.LoadScene(i); return true; }
        }
        error = "A cena '" + menuSceneName + "' não está incluída no Build Settings; integração com o menu pendente.";
        return false;
    }
}
