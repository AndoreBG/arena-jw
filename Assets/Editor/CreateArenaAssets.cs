#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace ArenaSetup
{
    /// <summary>
    /// Gera todos os ScriptableObjects do jogo de uma vez.
    ///
    /// Menu: <c>Arena &gt; Create ScriptableObjects Assets</c>.
    ///
    /// Saída:
    ///  • Assets/ScriptableObjects/Difficulties/DificuldadeFacil|Medio|Dificil.asset
    ///  • Assets/ScriptableObjects/DifficultyCatalog.asset (lista das três)
    ///  • Assets/ScriptableObjects/PlayerStats.asset
    ///  • Assets/ScriptableObjects/SpawnerConfig.asset
    ///  • Assets/ScriptableObjects/Resolutions/Resolucao_*.asset (6)
    ///  • Assets/Resources/ (cópias acessíveis em runtime por Resources.Load)
    /// </summary>
    public static class CreateArenaAssets
    {
        private static readonly (int width, int height)[] Resolutions =
        {
            (1280, 720),
            (1366, 768),
            (1600, 900),
            (1920, 1080),
            (2560, 1440),
            (3840, 2160)
        };

        [MenuItem("Arena/Create ScriptableObjects Assets", priority = 10)]
        public static void CreateAssets()
        {
            const string root = ArenaEditorUtil.ScriptableObjectsRoot;
            const string difficultiesPath = root + "/Difficulties";
            const string resolutionsPath = root + "/Resolutions";

            ArenaEditorUtil.EnsureFolder(root);
            ArenaEditorUtil.EnsureFolder(difficultiesPath);
            ArenaEditorUtil.EnsureFolder(resolutionsPath);
            ArenaEditorUtil.EnsureFolder(ArenaEditorUtil.ResourcesRoot);

            // --- Dificuldades ---
            DifficultyConfig facil = CreateDifficulty(difficultiesPath + "/DificuldadeFacil.asset",
                ArenaDifficulty.Facil, "Fácil", "IA fraca · 2 inimigos vivos · spawn a cada 3 s", 2, 3f, 0.35f);

            DifficultyConfig medio = CreateDifficulty(difficultiesPath + "/DificuldadeMedio.asset",
                ArenaDifficulty.Medio, "Médio", "IA média · 4 inimigos vivos · spawn a cada 2 s", 4, 2f, 0.65f);

            DifficultyConfig dificil = CreateDifficulty(difficultiesPath + "/DificuldadeDificil.asset",
                ArenaDifficulty.Dificil, "Difícil", "IA forte · 6 inimigos vivos · spawn a cada 1 s", 6, 1f, 1f);

            // --- Catálogo ---
            DifficultyCatalog catalog = ArenaEditorUtil.CreateOrLoadAsset<DifficultyCatalog>(root + "/DifficultyCatalog.asset");
            catalog.EditorConfigure(new List<DifficultyConfig> { facil, medio, dificil });
            EditorUtility.SetDirty(catalog);

            // --- PlayerStats ---
            PlayerStats stats = ArenaEditorUtil.CreateOrLoadAsset<PlayerStats>(root + "/PlayerStats.asset");
            EditorUtility.SetDirty(stats);

            // --- SpawnerConfig ---
            SpawnerConfig spawnerConfig = ArenaEditorUtil.CreateOrLoadAsset<SpawnerConfig>(root + "/SpawnerConfig.asset");
            spawnerConfig.EditorConfigure(medio, 18f, new Vector3(1.6f, 1.2f, 2f), new Vector3(0f, 3f, 0f));
            EditorUtility.SetDirty(spawnerConfig);

            // --- Resoluções ---
            foreach ((int width, int height) in Resolutions)
            {
                string path = $"{resolutionsPath}/Resolucao_{width}x{height}.asset";
                ResolutionOption option = ArenaEditorUtil.CreateOrLoadAsset<ResolutionOption>(path);
                option.EditorConfigure(width, height);
                EditorUtility.SetDirty(option);
            }

            AssetDatabase.SaveAssets();

            // --- Cópias em Resources (runtime) ---
            string resourcesPath = ArenaEditorUtil.ResourcesRoot;
            CopyToResources(catalog, resourcesPath + "/DifficultyCatalog.asset");
            CopyToResources(stats, resourcesPath + "/PlayerStats.asset");
            CopyToResources(spawnerConfig, resourcesPath + "/SpawnerConfig.asset");

            string resourcesResolutions = resourcesPath + "/Resolutions";
            ArenaEditorUtil.EnsureFolder(resourcesResolutions);
            foreach ((int width, int height) in Resolutions)
            {
                ResolutionOption source = AssetDatabase.LoadAssetAtPath<ResolutionOption>($"{resolutionsPath}/Resolucao_{width}x{height}.asset");
                CopyToResources(source, $"{resourcesResolutions}/Resolucao_{width}x{height}.asset");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);

            AudioMixerReport();

            Debug.Log("<b>[Arena] ScriptableObjects criados.</b>\n" +
                      "• 3 DifficultyConfig (Fácil: 2/3s · Médio: 4/2s · Difícil: 6/1s)\n" +
                      "• DifficultyCatalog, PlayerStats, SpawnerConfig\n" +
                      "• 6 ResolutionOption\n" +
                      "• Cópias em Assets/Resources para carregamento em runtime.");
        }

        private static DifficultyConfig CreateDifficulty(string path, ArenaDifficulty difficulty, string displayName,
            string description, int maxAlive, float spawnInterval, float intensity)
        {
            DifficultyConfig asset = ArenaEditorUtil.CreateOrLoadAsset<DifficultyConfig>(path);
            asset.EditorConfigure(difficulty, displayName, description, maxAlive, spawnInterval, intensity);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Confere se existe o mixer de áudio esperado (Resources/ArenaMixer com os parâmetros
        /// expostos). Não cria o mixer: o passo a passo está em Docs/AudioMixer.md.
        /// </summary>
        [MenuItem("Arena/Check Audio Mixer", priority = 95)]
        public static void AudioMixerReport()
        {
            AudioMixer mixer = Resources.Load<AudioMixer>(AudioSettings.MixerResourcesPath);
            if (mixer == null)
            {
                Debug.LogWarning("<b>[Arena] AudioMixer não encontrado</b> em Assets/Resources/" +
                                 AudioSettings.MixerResourcesPath + ".mixer.\n" +
                                 "Os volumes continuam salvando em PlayerPrefs e o master é aplicado em " +
                                 "AudioListener.volume. Para usar o mixer, siga Docs/AudioMixer.md.");
                return;
            }

            List<string> missing = new List<string>();
            foreach (string parameter in new[] { AudioSettings.MasterParameter, AudioSettings.SfxParameter, AudioSettings.MusicParameter })
                if (!mixer.GetFloat(parameter, out _)) missing.Add(parameter);

            if (missing.Count == 0)
                Debug.Log("<b>[Arena] AudioMixer OK.</b> Parâmetros expostos: " +
                          AudioSettings.MasterParameter + ", " + AudioSettings.SfxParameter + ", " + AudioSettings.MusicParameter + ".");
            else
                Debug.LogWarning("<b>[Arena] AudioMixer sem os parâmetros:</b> " + string.Join(", ", missing) +
                                 ". Exponha-os (clique direito no slider de volume > Expose) — veja Docs/AudioMixer.md.");
        }

        /// <summary>Copia (ou mantém sincronizada) uma cópia do asset em Resources.</summary>
        private static void CopyToResources(Object source, string destinationPath)
        {
            if (source == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath == destinationPath) return;

            if (AssetDatabase.LoadAssetAtPath<Object>(destinationPath) != null)
                AssetDatabase.DeleteAsset(destinationPath);

            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                Debug.LogWarning($"[Arena] Não foi possível copiar {sourcePath} para {destinationPath}.");
            else
                Debug.Log($"[Arena] Copiado para Resources: {destinationPath}");
        }
    }
}
#endif
