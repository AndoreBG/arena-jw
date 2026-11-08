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
            // Os assets podem estar em ScriptableObjects/ (fonte) ou só em Resources/ (cópia que o
            // jogo lê em runtime). Aceita os dois para não bloquear quem já rodou só um dos menus.
            DifficultyCatalog catalog = FindAsset<DifficultyCatalog>("DifficultyCatalog");
            PlayerStats stats = FindAsset<PlayerStats>("PlayerStats");
            SpawnerConfig spawnerConfig = FindAsset<SpawnerConfig>("SpawnerConfig");

            if (catalog == null || stats == null)
            {
                Debug.LogError("[Arena] DifficultyCatalog/PlayerStats não encontrados. " +
                               "Rode 'Arena > Create ScriptableObjects Assets'.");
                return null;
            }

            // Reaproveita o prefab anterior (se existir) para não perder ajustes e para o HUD
            // reconstruir por cima do Canvas já salvo.
            GameObject root;
            HUDController hud;
            ArenaGameOverUI gameOver;

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaEditorUtil.GameInitializerPath);
            if (existing != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(existing);
                root.name = "ArenaGame";

                hud = root.GetComponentInChildren<HUDController>(true);
                if (hud == null)
                {
                    GameObject hudObject = new GameObject("HUDController");
                    hudObject.transform.SetParent(root.transform, false);
                    hud = hudObject.AddComponent<HUDController>();
                }

                gameOver = root.GetComponentInChildren<ArenaGameOverUI>(true);
                if (gameOver == null)
                {
                    GameObject gameOverObject = new GameObject("GameOverUI");
                    gameOverObject.transform.SetParent(root.transform, false);
                    gameOver = gameOverObject.AddComponent<ArenaGameOverUI>();
                }
            }
            else
            {
                root = new GameObject("ArenaGame");

                GameObject hudObject = new GameObject("HUDController");
                hudObject.transform.SetParent(root.transform, false);
                hud = hudObject.AddComponent<HUDController>();

                GameObject gameOverObject = new GameObject("GameOverUI");
                gameOverObject.transform.SetParent(root.transform, false);
                gameOver = gameOverObject.AddComponent<ArenaGameOverUI>();
            }

            // A UI (HUD_Partida e GameOverPanel) é montada à mão no prefab ArenaGame.
            // Este menu NÃO reconstrói mais a hierarquia: ele só liga as referências.

            // Inicializador
            ArenaGameInitializer initializer = root.GetComponent<ArenaGameInitializer>();
            if (initializer == null) initializer = root.AddComponent<ArenaGameInitializer>();
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

        /// <summary>Procura o asset em ScriptableObjects/ e, se não achar, em Resources/.</summary>
        private static T FindAsset<T>(string fileName) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>($"{ArenaEditorUtil.ScriptableObjectsRoot}/{fileName}.asset");
            if (asset != null) return asset;

            asset = AssetDatabase.LoadAssetAtPath<T>($"{ArenaEditorUtil.ResourcesRoot}/{fileName}.asset");
            if (asset != null) return asset;

            string[] guids = AssetDatabase.FindAssets($"{fileName} t:{typeof(T).Name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"/{fileName}.asset")) return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return null;
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
