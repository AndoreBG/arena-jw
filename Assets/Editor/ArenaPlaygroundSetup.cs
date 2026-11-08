#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ArenaSetup
{
    /// <summary>
    /// Configura o Playground sem edição manual: reescreve o prefab <c>Inimigo.prefab</c> como
    /// nave alienígena e coloca o <c>ArenaGame</c> (HUD + Game Over + bootstrap) na cena.
    ///
    /// Menu: <c>Arena &gt; Setup Main Menu + Playground</c>.
    ///
    /// O prefab é reconstruído pela API do Unity (PrefabUtility), preservando os meshes, os
    /// materiais e os efeitos que já existiam. A cena é salva pela própria API do Editor, então
    /// o formato binário do arquivo não é um problema.
    /// </summary>
    public static class ArenaPlaygroundSetup
    {
        private const string TagPlayer = "Player";
        private const string LayerEnemy = "Enemy";

        [MenuItem("Arena/Setup Main Menu + Playground", priority = 0)]
        public static void SetupAll()
        {
            if (!EditorUtility.DisplayDialog("Arena",
                    "Este comando:\n\n" +
                    "1. Gera os ScriptableObjects (se faltarem).\n" +
                    "2. Reescreve o prefab Inimigo como nave alienígena.\n" +
                    "3. Coloca o ArenaGame (HUD + Game Over) no Playground.\n" +
                    "4. Salva a cena e atualiza o Build Settings.\n\n" +
                    "Continuar?",
                    "Configurar", "Cancelar"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Arena", "Criando ScriptableObjects...", 0.1f);
                CreateArenaAssets.CreateAssets();

                EditorUtility.DisplayProgressBar("Arena", "Criando prefab ArenaGame...", 0.3f);
                CreateArenaGamePrefab.CreatePrefab();

                EditorUtility.DisplayProgressBar("Arena", "Reescrevendo prefab Inimigo...", 0.5f);
                SetupEnemyPrefab();

                EditorUtility.DisplayProgressBar("Arena", "Configurando Playground...", 0.8f);
                SetupPlaygroundScene();

                SetupMenuScene();

                EditorUtility.DisplayProgressBar("Arena", "Atualizando Build Settings...", 0.95f);
                ArenaMenuSceneSetup.AddSceneToBuildSettings(ArenaEditorUtil.MenuScenePath);
                ArenaMenuSceneSetup.AddSceneToBuildSettings(ArenaEditorUtil.PlaygroundScenePath);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Arena] Falha na configuração: " + exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog("Arena",
                "Configuração concluída!\n\n" +
                "Falta apenas:\n" +
                "• Abrir o Playground e rebake da NavMesh (NavMesh Surface > Bake).\n" +
                "• Testar o fluxo: Menu > Jogar > dificuldade > Playground.\n\n" +
                "Com o jogo configurado, a nave flutua 3 m acima do chão e atira.",
                "OK");
        }

        #region Prefab do inimigo

        [MenuItem("Arena/Setup Enemy Prefab (Alien Ship)", priority = 30)]
        public static void SetupEnemyPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaEditorUtil.EnemyPrefabPath);
            if (source == null)
            {
                Debug.LogError("[Arena] Prefab não encontrado: " + ArenaEditorUtil.EnemyPrefabPath);
                return;
            }

            EnsureLayer(LayerEnemy);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null)
            {
                Debug.LogError("[Arena] Não foi possível instanciar o prefab Inimigo.");
                return;
            }

            try
            {
                // O prefab guarda só a arte (Cabine, Corpo) + Rigidbody + NavMeshAgent. Este
                // comando acrescenta o comportamento. Tudo abaixo é "pega se existir, senão cria",
                // então pode rodar quantas vezes quiser.

                // --- Tag e layer ---
                try { instance.tag = "Untagged"; }
                catch { Debug.LogWarning("[Arena] Tag Untagged indisponível."); }

                int enemyLayer = LayerMask.NameToLayer(LayerEnemy);
                instance.layer = enemyLayer >= 0 ? enemyLayer : 0;

                // --- Rigidbody: kinematic, porque quem move é o NavMeshAgent ---
                Rigidbody body3d = instance.GetComponent<Rigidbody>();
                if (body3d == null) body3d = instance.AddComponent<Rigidbody>();
                body3d.isKinematic = true;
                body3d.useGravity = false;
                body3d.detectCollisions = true;
                body3d.interpolation = RigidbodyInterpolation.None;

                // --- Colisor: é ele que recebe o tiro do jogador ---
                // Sem colisor, Physics.Linecast do tiro não acha a nave e ela nunca leva dano.
                BoxCollider hitBox = instance.GetComponent<BoxCollider>();
                if (hitBox == null) hitBox = instance.AddComponent<BoxCollider>();
                hitBox.isTrigger = false;
                hitBox.enabled = true;
                hitBox.center = new Vector3(0f, 0.75f, 0f);
                hitBox.size = new Vector3(3.4f, 1.8f, 4.2f);

                // --- NavMeshAgent: anda no plano; a altura vem do EnemyHover ---
                NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
                if (agent == null) agent = instance.AddComponent<NavMeshAgent>();
                agent.radius = 1.2f;
                agent.height = 2f;
                agent.baseOffset = 0f;
                agent.speed = 4f;
                agent.acceleration = 8f;
                agent.angularSpeed = 360f;
                agent.stoppingDistance = 0f;
                agent.autoBraking = false;
                agent.autoTraverseOffMeshLink = false;
                agent.updatePosition = true;    // o agente manda no X/Z
                agent.updateRotation = false;   // quem gira a nave é a AlienShipWeapon
                agent.updateUpAxis = false;

                // --- Vida: sem ela a nave não morre nem pontua ---
                // ArenaEnemyRegistration exige Vida no MESMO GameObject ([RequireComponent]).
                Vida life = instance.GetComponent<Vida>();
                if (life == null) life = instance.AddComponent<Vida>();
                ArenaEditorUtil.SetField(life, "vidaMaxima", 100f);
                ArenaEditorUtil.SetField(life, "destruirAoMorrer", false);

                // --- Sensor: quem a nave vê ---
                ArenaEnemySensor sensor = instance.GetComponent<ArenaEnemySensor>();
                if (sensor == null) sensor = instance.AddComponent<ArenaEnemySensor>();
                ArenaEditorUtil.SetField(sensor, "viewDistance", 26f);
                ArenaEditorUtil.SetField(sensor, "fieldOfView", 220f);
                ArenaEditorUtil.SetField(sensor, "blockingMask", ArenaGameInitializer.DefaultLineOfSightMask);
                ArenaEditorUtil.SetField(sensor, "eyes", instance.transform);

                // --- IA: patrulha, persegue e ataca ---
                AlienShipAI ai = instance.GetComponent<AlienShipAI>();
                if (ai == null) ai = instance.AddComponent<AlienShipAI>();
                ArenaEditorUtil.SetField(ai, "sensor", sensor);

                // --- Arma: atira da frente da nave ---
                AlienShipWeapon weapon = instance.GetComponent<AlienShipWeapon>();
                if (weapon == null) weapon = instance.AddComponent<AlienShipWeapon>();
                ArenaEditorUtil.SetField(weapon, "lineOfSightMask", ArenaGameInitializer.DefaultLineOfSightMask);

                // --- Flutuação: 3 m ± 0,5 m ---
                if (instance.GetComponent<EnemyHover>() == null) instance.AddComponent<EnemyHover>();

                // --- Barra de vida acima da nave ---
                EnemyHealthBar healthBar = instance.GetComponent<EnemyHealthBar>();
                if (healthBar == null) healthBar = instance.AddComponent<EnemyHealthBar>();
                ArenaEditorUtil.SetField(healthBar, "life", life);

                // --- Registro: liga a morte ao spawner e ao placar ---
                if (instance.GetComponent<ArenaEnemyRegistration>() == null)
                    instance.AddComponent<ArenaEnemyRegistration>();

                // Rede de seguranca: se o prefab ainda tiver componente com script ausente
                // (m_Script: {fileID: 0}), o Unity recusa salvar com "You are trying to save a
                // Prefab with a missing script". Remove antes de tentar.
                int broken = RemoveBrokenBehaviours(instance);
                if (broken > 0)
                    Debug.LogWarning($"[Arena] {broken} componente(s) sem script removido(s) antes de salvar.");

                PrefabUtility.SaveAsPrefabAsset(instance, ArenaEditorUtil.EnemyPrefabPath, out bool saveOk);

                if (!saveOk)
                {
                    Debug.LogError("[Arena] Não foi possível salvar o prefab Inimigo. " +
                                   "Feche o prefab se estiver aberto no Prefab Mode e tente de novo.");
                    return;
                }

                Debug.Log("<b>[Arena] Prefab Inimigo configurado.</b>\n" +
                          "• NavMeshAgent: " + (agent.updateRotation ? "gira sozinho" : "só anda no plano") +
                          " · altura de voo pelo EnemyHover\n" +
                          "• Componentes: Vida, ArenaEnemySensor, AlienShipAI, AlienShipWeapon, " +
                          "EnemyHover, EnemyHealthBar, ArenaEnemyRegistration\n" +
                          "• Tag: " + instance.tag + " · Layer: " + LayerMask.LayerToName(instance.layer));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        #endregion

        #region Cena do Playground

        [MenuItem("Arena/Setup Playground Scene", priority = 40)]
        /// <summary>
        /// Remove componentes cujo m_Script ficou vazio (fileID 0). O Unity se recusa a salvar
        /// um prefab nesse estado: "You are trying to save a Prefab with a missing script".
        /// </summary>
        private static int RemoveBrokenBehaviours(GameObject root)
        {
            int removed = 0;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count > 0)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    EditorUtility.SetDirty(go);
                    removed += count;
                }
            }

            return removed;
        }

        public static void SetupPlaygroundScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ArenaEditorUtil.PlaygroundScenePath, OpenSceneMode.Single);

            // Remove instalações antigas (idempotência).
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root.name == "ArenaGame" || root.name == "ArenaGameBootstrap")
                    Object.DestroyImmediate(root);
            }

            // 1. Prefab ArenaGame (HUD + Game Over + bootstrap).
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaEditorUtil.GameInitializerPath);
            if (prefab == null) prefab = CreateArenaGamePrefab.CreatePrefab();

            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "ArenaGame";
                instance.transform.position = Vector3.zero;

                // Liga as referências de cena que não podem viver no prefab.
                ArenaGameInitializer initializer = instance.GetComponent<ArenaGameInitializer>();
                HUDController hud = instance.GetComponentInChildren<HUDController>(true);
                ArenaGameOverUI gameOver = instance.GetComponentInChildren<ArenaGameOverUI>(true);

                ArenaEditorUtil.SetField(initializer, "hud", hud);
                ArenaEditorUtil.SetField(initializer, "gameOver", gameOver);
            }

            // 2. EventSystem compatível com Input System.
            ArenaEditorUtil.EnsureEventSystem();

            // 3. Jogador: o prefab Inimigo antigo tinha a tag Player; garante que o tanque tem.
            GameObject player = GameObject.FindGameObjectWithTag(TagPlayer);
            if (player != null)
            {
                Vida life = player.GetComponentInParent<Vida>();
                if (life != null) ArenaEditorUtil.SetField(life, "destruirAoMorrer", false);
            }
            else
            {
                Debug.LogWarning("[Arena] Nenhum objeto com a tag 'Player' no Playground. " +
                                 "Marque o tanque do jogador com a tag Player.");
            }

            // 4. Pool de projéteis única na cena (o player cria uma se não existir).
            EnsurePoolInScene();
            EnsureSpawnPointsInScene(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<b>[Arena] Playground salvo</b> com ArenaGame (HUD, Game Over e bootstrap).\n" +
                      "Próximo passo manual obrigatório: selecione o NavMesh Surface e clique em Bake.");
        }

        private static void EnsurePoolInScene()
        {
            foreach (TankProjectilePool pool in Object.FindObjectsByType<TankProjectilePool>(FindObjectsSortMode.None))
                return;

            ArenaEditorUtil.EnsureFolder("Assets/Resources");
            TankProjectile prefab = Resources.Load<TankProjectile>("ProjetilTanque");

            GameObject go = new GameObject("PoolDeProjetis");
            TankProjectilePool component = go.AddComponent<TankProjectilePool>();

            if (prefab != null) ArenaEditorUtil.SetField(component, "projectilePrefab", prefab);
            else
            {
                string[] guids = AssetDatabase.FindAssets("ProjetilTanque t:Prefab");
                foreach (string guid in guids)
                {
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    TankProjectile projectile = asset != null ? asset.GetComponent<TankProjectile>() : null;
                    if (projectile != null) { ArenaEditorUtil.SetField(component, "projectilePrefab", projectile); break; }
                }
            }
        }

        private static void EnsureSpawnPointsInScene(GameObject player)
        {
            if (GameObject.Find("PontosDeSpawn") != null) return;
            if (player == null) return;

            GameObject group = new GameObject("PontosDeSpawn");
            const int count = 8;
            int created = 0;

            for (int index = 0; index < count; index++)
            {
                float angle = index * Mathf.PI * 2f / count;
                Vector3 candidate = player.transform.position +
                                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 32f;

                // Só cria o ponto onde a nave realmente cabe: NavMesh válido e espaço livre na altura do voo.
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 8f, NavMesh.AllAreas)) continue;
                if (Physics.CheckBox(hit.position + Vector3.up * 3f, new Vector3(1.6f, 1.2f, 2f),
                        Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;

                created++;
                GameObject point = new GameObject("SpawnPoint_" + created);
                point.transform.SetParent(group.transform, false);
                point.transform.position = hit.position;
                point.transform.LookAt(new Vector3(player.transform.position.x, hit.position.y, player.transform.position.z));
            }

            if (created == 0)
            {
                Object.DestroyImmediate(group);
                Debug.LogWarning("[Arena] Não foi possível criar pontos de spawn válidos. " +
                                 "O spawner usará pontos aleatórios do NavMesh ao redor do jogador.");
            }
            else
            {
                Debug.Log($"[Arena] {created} pontos de spawn criados ao redor do jogador. Ajuste-os no Inspector se preciso.");
            }
        }

        private static void SetupMenuScene()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaEditorUtil.MenuScenePath) != null;
            if (exists)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(ArenaEditorUtil.MenuScenePath, OpenSceneMode.Single);
            }

            ArenaMenuSceneSetup.SetupMainMenuScene();
        }

        #endregion

        #region Utilitários

        private static void RemoveIfPresent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component);
        }

        private static Transform FindChildContaining(Transform root, string fragment)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;
            return null;
        }

        /// <summary>Cria o layer "Enemy" (posição 9) se ele ainda não existir.</summary>
        private static void EnsureLayer(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0) return;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int index = 8; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;

                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log("[Arena] Layer criado: " + layerName + " (índice " + index + ").");
                return;
            }

            Debug.LogWarning("[Arena] Sem espaço livre em Project Settings > Tags and Layers para o layer 'Enemy'.");
        }

        #endregion
    }
}
#endif
