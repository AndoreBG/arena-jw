using UnityEngine;

/// <summary>
/// Ajustes de spawn que não mudam por dificuldade. Não guarda referências de cena
/// (ScriptableObjects não podem apontar para objetos de cena); os pontos de spawn ficam no
/// <see cref="ArenaSpawner"/>.
/// </summary>
[CreateAssetMenu(menuName = "Arena/Spawner Config", fileName = "SpawnerConfig")]
public sealed class SpawnerConfig : ScriptableObject
{
    [Tooltip("Dificuldade (e, com ela, limite de vivos e intervalo de spawn) usada pelo spawner.")]
    [SerializeField] private DifficultyConfig difficulty;

    [Tooltip("Distância horizontal mínima entre o ponto de spawn e o jogador.")]
    [SerializeField, Min(0f)] private float minimumPlayerDistance = 18f;

    [Tooltip("Meia-extensão da caixa de ocupação testada antes de criar o inimigo.")]
    [SerializeField] private Vector3 occupancyHalfExtents = new Vector3(1.6f, 1.2f, 2f);

    [Tooltip("Deslocamento da caixa de ocupação a partir do ponto no NavMesh. Deve ficar acima do chão " +
             "(a nave voa a 3 m), senão a caixa encosta no piso e o spawn é recusado para sempre.")]
    [SerializeField] private Vector3 occupancyOffset = new Vector3(0f, 3f, 0f);

    [Header("Fallback sem pontos de spawn")]
    [Tooltip("Usa pontos aleatórios do NavMesh num anel ao redor do jogador quando nenhum ponto fixo foi configurado.")]
    [SerializeField] private bool useRandomNavMeshPoints = true;

    [SerializeField, Min(1f)] private float ringRadiusMin = 28f;
    [SerializeField, Min(1f)] private float ringRadiusMax = 45f;

    public DifficultyConfig Difficulty => difficulty;
    public float MinimumPlayerDistance => minimumPlayerDistance;
    public Vector3 OccupancyHalfExtents => occupancyHalfExtents;
    public Vector3 OccupancyOffset => occupancyOffset;
    public bool UseRandomNavMeshPoints => useRandomNavMeshPoints;
    public float RingRadiusMin => ringRadiusMin;
    public float RingRadiusMax => Mathf.Max(ringRadiusMin + 1f, ringRadiusMax);

    public float SpawnInterval => difficulty != null ? difficulty.SpawnInterval : 3f;
    public int MaxAliveEnemies => difficulty != null ? difficulty.MaxAliveEnemies : 2;

    public void ApplyDifficultyToEnemy(AlienShipAI ai, ArenaEnemySensor sensor, AlienShipWeapon weapon)
    {
        if (difficulty != null) difficulty.ApplyToEnemy(ai, sensor, weapon);
    }

#if UNITY_EDITOR
    public void EditorConfigure(DifficultyConfig value, float minPlayerDistance, Vector3 halfExtents, Vector3 offset)
    {
        difficulty = value;
        minimumPlayerDistance = Mathf.Max(0f, minPlayerDistance);
        occupancyHalfExtents = halfExtents;
        occupancyOffset = offset;
    }
#endif
}
