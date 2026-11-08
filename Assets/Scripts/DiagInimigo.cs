// DIAGNOSTICO TEMPORARIO do disparo do inimigo.
//
// COMO USAR:
//   1. Copie este arquivo para Assets/Scripts/ (qualquer pasta do projeto serve).
//   2. Abra o playground e aperte Play.
//   3. Olhe o Console. Ele imprime a cada 2 s o que esta travando o tiro.
//   4. Apague este arquivo depois de resolver.
//
// Ele NAO altera nada: so le o estado e escreve no Console.
using UnityEngine;
using UnityEngine.AI;

public sealed class DiagInimigo : MonoBehaviour
{
    [Tooltip("Inimigo so atira se a distancia for menor que fireRange (40 no seu prefab).")]
    public float fireRangeEsperado = 40f;

    private float timer;
    private int rodadas;

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = 2f;

        rodadas++;
        if (rodadas == 1)
            Diagnostico();
        else
            SoOEssencial();
    }

    // ------------------------------------------------------------------ completo
    private void Diagnostico()
    {
        Debug.Log("========== DIAGNOSTICO DO INIMIGO ==========");

        // [1] Manager
        ArenaManager mgr = ArenaManager.Instance;
        Debug.Log($"[1] ArenaManager: {(mgr == null ? "NAO EXISTE NA CENA" : mgr.name)}" +
                  $" | IsPlaying={ArenaManager.IsPlaying}");

        // [2] Pool de projeteis: se o prefab dela estiver vazio, NINGUEM atira.
        TankProjectilePool pool = TankProjectilePool.Instance;
        if (pool == null)
        {
            Debug.LogError("[2] TankProjectilePool NAO ENCONTRADO na cena.");
            Debug.LogError("    >>> Sem pool, nem o jogador atira. Verifique o objeto 'ProjectilePool'.");
        }
        else
        {
            object prefab = CampoPrivado(pool, "projectilePrefab");
            Debug.Log($"[2] Pool '{pool.name}' | projectilePrefab={(prefab == null ? "VAZIO -> todo tiro falha" : "ok")}");
            if (prefab == null)
                Debug.LogError("    >>> Arraste o ProjetilTanque no campo Projectile Prefab da pool.");
        }

        // [3] Naves
        AlienShipAI[] naves = FindObjectsByType<AlienShipAI>(FindObjectsSortMode.None);
        Debug.Log($"[3] Naves vivas na cena: {naves.Length}");
        if (naves.Length == 0)
        {
            Debug.LogError("    >>> Nenhuma nave existe. O spawner nao esta instanciando.");
            Debug.LogError("    >>> Confira: ArenaSpawner ativo? SpawnerConfig com enemyPrefab? NavMesh assado?");
            return;
        }

        foreach (AlienShipAI ai in naves)
            DetalharNave(ai, true);
    }

    // ------------------------------------------------------------------ resumido
    private void SoOEssencial()
    {
        foreach (AlienShipAI ai in FindObjectsByType<AlienShipAI>(FindObjectsSortMode.None))
        {
            AlienShipWeapon w = ai.GetComponent<AlienShipWeapon>();
            Transform alvo = w != null ? w.Target : null;
            string est = Estado(ai);

            string motivo = null;
            if (alvo == null) motivo = "arma SEM ALVO";
            else if (!w.CanFire) motivo = "canFire=false (nao esta em Attack)";
            else
            {
                float d = Vector3.Distance(ai.transform.position, alvo.position);
                if (d > fireRangeEsperado) motivo = $"longe demais ({d:F0} m > {fireRangeEsperado})";
            }

            Debug.Log($"[nave] {ai.name} | {est} | " +
                      $"{(motivo == null ? "TUDO OK, deveria estar atirando" : motivo)}");
        }
    }

    private void DetalharNave(AlienShipAI ai, bool completo)
    {
        AlienShipWeapon w = ai.GetComponent<AlienShipWeapon>();
        ArenaEnemySensor sensor = ai.GetComponent<ArenaEnemySensor>();
        NavMeshAgent agent = ai.GetComponent<NavMeshAgent>();
        Transform alvo = w != null ? w.Target : null;

        Debug.Log($"[4] {ai.name}");
        Debug.Log($"    estado = {Estado(ai)}");
        Debug.Log($"    alvo da arma = {(alvo == null ? "NULL  <<< arma nunca atira" : alvo.name)}");
        Debug.Log($"    canFire = {(w != null ? w.CanFire.ToString() : "SEM ARMA")}" +
                  $"   (so vira true no estado Attack)");

        if (agent == null)
            Debug.LogError("    SEM NavMeshAgent");
        else
            Debug.Log($"    agent: enabled={agent.enabled} onNavMesh={agent.isOnNavMesh} " +
                      $"speed={agent.speed:F1} baseOffset={agent.baseOffset:F2}");

        if (alvo != null && w != null)
        {
            float d = Vector3.Distance(ai.transform.position, alvo.position);
            string dentro = d <= fireRangeEsperado ? "dentro" : "FORA";
            Debug.Log($"    distancia = {d:F1} m ({dentro} do fireRange {fireRangeEsperado})");
        }

        if (sensor != null)
        {
            ArenaEnemySnapshot snap = sensor.Snapshot;
            Debug.Log($"    sensor: visivel={snap.visible} distancia={snap.distance:F1} " +
                      $"jaViu={(snap.hasLastSeenPosition ? "sim" : "nao")}");
        }

        Debug.Log($"    posicao: {ai.transform.position}");
    }

    // ------------------------------------------------------------------ helpers
    private static string Estado(AlienShipAI ai)
    {
        object v = CampoPrivado(ai, "state");
        return v == null ? "?" : v.ToString();
    }

    private static object CampoPrivado(object obj, string nome)
    {
        if (obj == null) return null;
        var campo = obj.GetType().GetField(nome,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return campo?.GetValue(obj);
    }
}
