using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject npcPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxNPCs = 10;

    [Header("Arena (baseado no Transform deste objeto)")]
    [SerializeField] private Vector3 arenaSize = new Vector3(20f, 0f, 20f);

    private float nextSpawnTime = 0f;
    private int currentNPCCount = 0;

    private void Update()
    {
        if (currentNPCCount >= maxNPCs) return;
        if (Time.time < nextSpawnTime) return;

        SpawnNPC();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void SpawnNPC()
    {
        Vector3 spawnPosition = GetRandomPositionInArena();

        GameObject npc = Instantiate(npcPrefab, spawnPosition, Quaternion.identity);

        NPCTracker tracker = npc.AddComponent<NPCTracker>();
        tracker.OnNPCDestroyed += HandleNPCDestroyed;

        currentNPCCount++;

        Debug.Log($"NPC spawned em {spawnPosition} | Total: {currentNPCCount}/{maxNPCs}");
    }

    private Vector3 GetRandomPositionInArena()
    {
        float halfX = arenaSize.x / 2f;
        float halfZ = arenaSize.z / 2f;

        float x = Random.Range(-halfX, halfX);
        float z = Random.Range(-halfZ, halfZ);
        float y = arenaSize.y;

        return transform.position + new Vector3(x, y, z);
    }

    private void HandleNPCDestroyed()
    {
        currentNPCCount = Mathf.Max(0, currentNPCCount - 1);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + new Vector3(0f, arenaSize.y / 2f, 0f);
        Vector3 size = new Vector3(arenaSize.x, Mathf.Max(arenaSize.y, 0.1f), arenaSize.z);

        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
}
