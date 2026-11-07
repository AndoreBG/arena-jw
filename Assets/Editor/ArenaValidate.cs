#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArenaSetup
{
    /// <summary>
    /// Verificação de configuração. Menu: <c>Arena &gt; Validate Setup</c>.
    /// Aponta referências faltando na cena aberta e nos prefabs gerados.
    /// </summary>
    public static class ArenaValidate
    {
        [MenuItem("Arena/Validate Setup", priority = 90)]
        public static void Validate()
        {
            List<string> problems = new List<string>();

            // --- Assets ---
            if (AssetDatabase.LoadAssetAtPath<DifficultyCatalog>(ArenaEditorUtil.ScriptableObjectsRoot + "/DifficultyCatalog.asset") == null)
                problems.Add("DifficultyCatalog não existe. Rode 'Arena > Create ScriptableObjects Assets'.");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaEditorUtil.GameInitializerPath) == null)
                problems.Add("Prefab ArenaGame não existe. Rode 'Arena > Setup Main Menu + Playground'.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaEditorUtil.MenuScenePath) == null)
                problems.Add("Cena menu_principal não existe. Rode 'Arena > Setup Main Menu Scene'.");

            // --- Build Settings ---
            if (!IsInBuildSettings(ArenaEditorUtil.MenuScenePath)) problems.Add("menu_principal não está no Build Settings.");
            if (!IsInBuildSettings(ArenaEditorUtil.PlaygroundScenePath)) problems.Add("playground não está no Build Settings.");

            // --- Cena aberta ---
            Scene scene = SceneManager.GetActiveScene();
            Debug.Log($"<b>[Arena] Validando cena aberta: {scene.name}</b>");

            if (string.Equals(scene.name, "playground", System.StringComparison.OrdinalIgnoreCase))
            {
                if (GameObject.FindGameObjectWithTag("Player") == null) problems.Add("Nenhum objeto com a tag 'Player' na cena.");
                if (Object.FindFirstObjectByType<ArenaGameInitializer>() == null) problems.Add("Nenhum ArenaGameInitializer na cena (HUD/Game Over não serão montados se ele não existir).");
                if (Object.FindFirstObjectByType<NavMeshSurface>() == null) problems.Add("Nenhum NavMeshSurface na cena (bake necessário).");
            }

            if (string.Equals(scene.name, "MenuPrincipal", System.StringComparison.OrdinalIgnoreCase))
            {
                if (Object.FindFirstObjectByType<MenuManager>() == null) problems.Add("MenuManager ausente na cena de menu.");
                if (Object.FindFirstObjectByType<RankingDisplay>() == null) problems.Add("RankingDisplay ausente na cena de menu.");
                if (Object.FindFirstObjectByType<SettingsPanel>() == null) problems.Add("SettingsPanel ausente na cena de menu.");
            }

            if (problems.Count == 0)
                Debug.Log("<b>[Arena] Nenhum problema encontrado.</b>");
            else
                Debug.LogWarning("<b>[Arena] Problemas encontrados:</b>\n• " + string.Join("\n• ", problems));
        }

        private static bool IsInBuildSettings(string path)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path == path && scene.enabled) return true;
            return false;
        }

        /// <summary>Relatório rápido do menu + playground, sem abrir as cenas.</summary>
        [MenuItem("Arena/Validate All Scenes", priority = 91)]
        public static void ValidateAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene current = SceneManager.GetActiveScene();
            string currentPath = current.path;

            foreach (string path in new[] { ArenaEditorUtil.MenuScenePath, ArenaEditorUtil.PlaygroundScenePath })
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Validate();
            }

            if (!string.IsNullOrEmpty(currentPath))
                EditorSceneManager.OpenScene(currentPath, OpenSceneMode.Single);
        }
    }
}
#endif
