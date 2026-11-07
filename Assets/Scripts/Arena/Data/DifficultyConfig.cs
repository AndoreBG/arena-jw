using UnityEngine;

/// <summary>Dificuldades do jogo. A ordem dos valores é serializada em PlayerPrefs e no ranking.</summary>
public enum ArenaDifficulty { Facil = 0, Medio = 1, Dificil = 2 }

/// <summary>
/// Números de uma dificuldade. Os assets são gerados por
/// <c>Arena &gt; Create ScriptableObjects Assets</c> em <c>Assets/ScriptableObjects/Difficulties</c>.
/// </summary>
[CreateAssetMenu(menuName = "Arena/Difficulty Config", fileName = "Dificuldade")]
public sealed class DifficultyConfig : ScriptableObject
{
    [Header("Identificação")]
    [SerializeField] private ArenaDifficulty difficulty = ArenaDifficulty.Facil;
    [SerializeField] private string displayName = "Fácil";
    [SerializeField, TextArea(2, 3)] private string description = "IA fraca · 2 inimigos · spawn lento";

    [Header("Números")]
    [Tooltip("Quantidade máxima de inimigos vivos ao mesmo tempo.")]
    [SerializeField, Min(1)] private int maxAliveEnemies = 2;

    [Tooltip("Segundos entre tentativas de spawn.")]
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;

    [Tooltip("0 = IA fraca, 1 = IA forte. Escala a visão, o alcance de ataque e a cadência da nave.")]
    [SerializeField, Range(0f, 1f)] private float aiIntensity = 0.5f;

    public ArenaDifficulty Difficulty => difficulty;
    public string DisplayName => displayName;
    public string Description => description;
    public int MaxAliveEnemies => maxAliveEnemies;
    public float SpawnInterval => spawnInterval;
    public float AiIntensity => aiIntensity;

    /// <summary>Aplica a intensidade da IA nos componentes de um inimigo recém-criado.</summary>
    public void ApplyToEnemy(AlienShipAI ai, ArenaEnemySensor sensor, AlienShipWeapon weapon)
    {
        if (sensor != null) sensor.ViewDistanceMultiplier = 0.75f + 0.5f * aiIntensity;
        if (ai != null) ai.Aggression = aiIntensity;
        if (weapon != null)
        {
            weapon.SetFireIntervalMultiplier(Mathf.Lerp(1.6f, 1f, aiIntensity));
            weapon.SetAimTolerance(Mathf.Lerp(16f, 8f, aiIntensity)); // IA fraca erra mais
        }
    }

#if UNITY_EDITOR
    /// <summary>Usado apenas pelo gerador de assets do Editor.</summary>
    public void EditorConfigure(ArenaDifficulty value, string name, string info, int maxAlive, float interval, float intensity)
    {
        difficulty = value;
        displayName = name;
        description = info;
        maxAliveEnemies = Mathf.Max(1, maxAlive);
        spawnInterval = Mathf.Max(0.1f, interval);
        aiIntensity = Mathf.Clamp01(intensity);
    }
#endif
}
