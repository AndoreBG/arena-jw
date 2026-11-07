#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ArenaSetup
{
    /// <summary>
    /// Cria (ou atualiza) o prefab <c>Assets/Prefabs/ArenaGame.prefab</c>: o objeto que leva o
    /// HUD e o painel de Game Over para dentro do Playground de forma persistente.
    ///
    /// Menu: <c>Arena &gt; Create ArenaGame Prefab</c>.
    /// Rodado também por <c>Arena &gt; Setup Main Menu + Playground</c>.
    /// </summary>
    public static class CreateArenaGamePrefab
    {
        public static GameObject CreatePrefab()
        {
            DifficultyCatalog catalog = AssetDatabase.LoadAssetAtPath<DifficultyCatalog>(
                ArenaEditorUtil.ScriptableObjectsRoot + "/DifficultyCatalog.asset");
            PlayerStats stats = AssetDatabase.LoadAssetAtPath<PlayerStats>(
                ArenaEditorUtil.ScriptableObjectsRoot + "/PlayerStats.asset");
            SpawnerConfig spawnerConfig = AssetDatabase.LoadAssetAtPath<SpawnerConfig>(
                ArenaEditorUtil.ScriptableObjectsRoot + "/SpawnerConfig.asset");

            if (catalog == null || stats == null)
            {
                Debug.LogError("[Arena] Rode primeiro 'Arena > Create ScriptableObjects Assets'.");
                return null;
            }

            GameObject root = new GameObject("ArenaGame");

            // HUD
            GameObject hudObject = new GameObject("HUDController");
            hudObject.transform.SetParent(root.transform, false);
            HUDController hud = hudObject.AddComponent<HUDController>();
            hud.BuildHierarchy();

            // Painel de Game Over
            GameObject gameOverObject = new GameObject("GameOverUI");
            gameOverObject.transform.SetParent(root.transform, false);
            ArenaGameOverUI gameOver = gameOverObject.AddComponent<ArenaGameOverUI>();
            gameOver.BuildHierarchy();

            // Inicializador
            ArenaGameInitializer initializer = root.AddComponent<ArenaGameInitializer>();
            ArenaEditorUtil.SetField(initializer, "difficultyCatalog", catalog);
            ArenaEditorUtil.SetField(initializer, "playerStats", stats);
            ArenaEditorUtil.SetField(initializer, "spawnerConfig", spawnerConfig);
            ArenaEditorUtil.SetField(initializer, "hud", hud);
            ArenaEditorUtil.SetField(initializer, "gameOver", gameOver);

            ArenaEditorUtil.EnsureFolder("Assets/Prefabs");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ArenaEditorUtil.GameInitializerPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success || prefab == null)
            {
                Debug.LogError("[Arena] Falha ao salvar o prefab ArenaGame.");
                return null;
            }

            Debug.Log("[Arena] Prefab criado/atualizado: " + ArenaEditorUtil.GameInitializerPath);
            return prefab;
        }

        [MenuItem("Arena/Create ArenaGame Prefab", priority = 25)]
        public static void CreatePrefabFromMenu()
        {
            if (CreatePrefab() != null)
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(ArenaEditorUtil.GameInitializerPath));
        }
    }
}
#endif
