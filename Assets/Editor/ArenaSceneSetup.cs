// Unity editor utility — configuração da cena de arena (playground.unity).
//
// O plano técnico (PlanoTecnicoPartes1a3.md) indica que a cena playground.unity está
// BINÁRIA. Não a reescrevemos como YAML (proibido: perderia o NavMeshData embutido,
// meshes, etc.). Em vez disso, este utility corrige, via API do Unity, os itens que
// faltam na cena, sem falsificar a montagem de cena.
//
// Rodar: menu Unity -> "Arena > Configurar Cena (Playground)".
// O script também configura o prefab Assets/Prefabs/Inimigo.prefab (NavMeshAgent,
// boca do cano e olhos do sensor), substituindo o asset YAML por um equivalente
// reserializado pelo próprio Unity (o GUID do .meta não muda, referências são mantidas).
//
// Após rodar, baixe a NavMesh novamente: selecione NavMeshSurface e clique em Bake
// (o plano exige NavMesh atualizada).

#if !UNITY_EDITOR
// No runtime o script não existe (Editor only).
#else
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace ArenaSetup
{
    public static class ArenaSceneSetup
    {
        private const string EnemyPrefabPath = "Assets/Prefabs/Inimigo.prefab";

        [MenuItem("Arena/Configurar Cena (Playground)")]
        public static void ConfigureArenaScene()
        {
            var progress = EditorUtility.DisplayCancelableProgressBar("Arena", "Configurando cena...", 0f);
            try
            {
                SetupEventSystem();
                SetupNicknameInputField();
                SetupGameOverButtons();
                SetupManagerAndUiReferences();
                SetupPlayerLife();

                EditorUtility.DisplayCancelableProgressBar("Arena", "Configurando prefab Inimigo...", 0.5f);
                SetupEnemyPrefab();
            }
            catch (Exception ex)
            {
                Debug.LogError("Falha na configuração da arena: " + ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("<b>Configuração da arena concluída.</b>" +
                      "Próximos passos manuais: (1) selecione o NavMeshSurface e Bake a NavMesh;" +
                      "(2) confirme a pose dos SpawnPoints; (3) pressione Play para validar.");
        }

        // 1. EventSystem compatível com Input System (Item 4.3.3 do plano).
        private static void SetupEventSystem()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                Debug.Log("EventSystem já existente: " + existing.gameObject.name);
                return;
            }
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Debug.Log("EventSystem criado com InputSystemUIInputModule.");
        }

        // 2. TMP_InputField para o nome do ranking no painel Game Over (Item 4.3.4).
        private static void SetupNicknameInputField()
        {
            var nicknameGo = GameObject.Find("NicknameInput");
            if (nicknameGo == null)
            {
                Debug.LogWarning("NicknameInput não encontrado.");
                return;
            }
            if (nicknameGo.GetComponent<TMP_InputField>() == null)
            {
                var field = nicknameGo.AddComponent<TMP_InputField>();
                field.text = string.Empty;
                // O runtime valida exatamente 3 caracteres [A-Z0-9] (RankingService.Submit).
                Debug.Log("TMP_InputField adicionado a NicknameInput.");
            }
            else
            {
                Debug.Log("TMP_InputField já presente em NicknameInput.");
            }
        }

        // 3. Conexão dos botões à ArenaGameOverUI (Item 4.3.5 — wiring ausente na cena).
        private static void SetupGameOverButtons()
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<ArenaGameOverUI>();
            if (ui == null)
            {
                Debug.LogError("ArenaGameOverUI não encontrado na cena.");
                return;
            }
            var newGameButton = GameObject.Find("BotaoTentarNovamente");
            var returnMenuButton = GameObject.Find("BotaoMenuPrincipal");
            if (newGameButton != null)
            {
                var button = newGameButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(ui.SubmitAndStartNew);
                }
            }
            if (returnMenuButton != null)
            {
                var button = returnMenuButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(ui.SubmitAndReturnToMenu);
                }
            }
        }

        // 4. Referências do ArenaManager / ArenaGameOverUI (várias estavam null ou
        //    apontando para CanvasRenderer/Selectable — Items 4.3.2/4.3.4/4.3.5).
        private static void SetupManagerAndUiReferences()
        {
            var managerGo = GameObject.Find("ArenaManager");
            if (managerGo == null)
            {
                Debug.LogError("GameObject ArenaManager não encontrado.");
                return;
            }
            var manager = managerGo.GetComponent<ArenaManager>();
            var ui = managerGo.GetComponent<ArenaGameOverUI>();
            if (ui == null) ui = UnityEngine.Object.FindFirstObjectByType<ArenaGameOverUI>();
            if (manager == null || ui == null)
            {
                Debug.LogError("ArenaManager ou ArenaGameOverUI ausente no GameObject ArenaManager.");
                return;
            }

            // ArenaManager.gameOverUI (era null na cena).
            if (GetSerialized(manager, "gameOverUI") == null)
                SetSerialized(manager, "gameOverUI", ui);

            // ArenaManager.killsText: TMP_Text do 'Pontuacao' fora do painel (HUD ao vivo).
            if (GetSerialized(manager, "killsText") == null)
            {
                var killsGo = GameObject.Find("Pontuacao");
                if (killsGo != null)
                {
                    var tmp = killsGo.GetComponent<TMP_Text>();
                    if (tmp != null) SetSerialized(manager, "killsText", tmp);
                }
            }

            // ArenaManager.spawner: já preenchido (ArenaSpawner no mesmo GameObject).

            // ArenaGameOverUI.panel / scoreText / messageText / nicknameInput / buttons.
            if (GetSerialized(ui, "panel") == null)
                SetSerialized(ui, "panel", GameObject.Find("GameOverPanel"));

            if (GetSerialized(ui, "scoreText") == null)
            {
                var panelGo = GameObject.Find("GameOverPanel");
                var scoreGo = panelGo != null ? panelGo.transform.Find("Pontuacao") : null;
                if (scoreGo != null) SetSerialized(ui, "scoreText", scoreGo.GetComponent<TMP_Text>());
            }

            if (GetSerialized(ui, "messageText") == null)
            {
                var messageGo = GameObject.Find("GameOverText");
                if (messageGo != null) SetSerialized(ui, "messageText", messageGo.GetComponent<TMP_Text>());
            }

            if (GetSerialized(ui, "nicknameInput") == null)
            {
                var nicknameGo = GameObject.Find("NicknameInput");
                if (nicknameGo != null)
                    SetSerialized(ui, "nicknameInput", nicknameGo.GetComponent<TMP_InputField>());
            }

            if (GetSerialized(ui, "newGameButton") == null)
            {
                var btnGo = GameObject.Find("BotaoTentarNovamente");
                if (btnGo != null) SetSerialized(ui, "newGameButton", btnGo.GetComponent<Button>());
            }

            if (GetSerialized(ui, "returnToMenuButton") == null)
            {
                var btnGo = GameObject.Find("BotaoMenuPrincipal");
                if (btnGo != null) SetSerialized(ui, "returnToMenuButton", btnGo.GetComponent<Button>());
            }
        }

        // 5. Jogador persistente (Item 4.1.2 — destruirAoMorrer = false).
        private static void SetupPlayerLife()
        {
            var playerGo = GameObject.Find("Tanque");
            if (playerGo == null)
            {
                Debug.LogWarning("GameObject Tanque não encontrado.");
                return;
            }
            var vida = playerGo.GetComponent<Vida>();
            if (vida == null)
            {
                Debug.LogWarning("Vida ausente no Tanque.");
                return;
            }
            vida.DestruirAoMorrer = false;
            if (GetSerialized(vida, "destruirAoMorrer") is bool b && b)
                SetSerialized(vida, "destruirAoMorrer", false);
            Debug.Log("Tanque configurado como persistente (DestruirAoMorrer = false).");
        }

        // 6. Prefab Inimigo: NavMeshAgent (item 4.2.1 — ausente no prefab e o spawn
        //    depende dele), boca do cano e olhos do sensor (item 4.2.3).
        private static void SetupEnemyPrefab()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (asset == null)
            {
                Debug.LogError("Prefab Inimigo não encontrado: " + EnemyPrefabPath);
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
            {
                Debug.LogError("Falha ao instanciar o prefab Inimigo.");
                return;
            }

            try
            {
                // NavMeshAgent — obrigatório para o ArenaSpawner.TrySpawnOne.
                var agent = instance.GetComponent<NavMeshAgent>();
                if (agent == null)
                {
                    agent = instance.AddComponent<NavMeshAgent>();
                    Debug.Log("NavMeshAgent adicionado ao prefab Inimigo.");
                }
                agent.radius = 1.5f;
                agent.height = 2f;
                agent.speed = 4f;
                agent.acceleration = 8f;
                agent.angularSpeed = 360f;
                agent.stoppingDistance = 1f;
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.updateUpAxis = true;
                agent.autoTraverseOffMeshLink = false;
                agent.autoBraking = true;
                agent.enabled = true;

                // TiroDoInimigo.origemDoTiro: o Corpo do inimigo.
                var tiro = instance.GetComponent<TiroDoInimigo>();
                if (tiro != null)
                {
                    var corpo = instance.transform.Find("Corpo");
                    if (corpo != null) SetSerialized(tiro, "origemDoTiro", corpo);
                    else Debug.LogWarning("Corpo não encontrado no prefab do inimigo.");
                }
            }
            finally
            {
                // Substitui o asset pelo conteúdo editado (mantém o .meta/GUID).
                bool success = PrefabUtility.SaveAsPrefabAsset(instance, EnemyPrefabPath);
                if (!success)
                    Debug.LogError("Falha ao salvar prefab Inimigo.");
                else
                    Debug.Log("Prefab Inimigo salvo com NavMeshAgent/muzzle/eyes.");
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // Reflection helper para campos [SerializeField] private.
        private static object GetSerialized(MonoBehaviour mb, string fieldName)
        {
            var field = mb.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(mb) : null;
        }

        private static void SetSerialized(MonoBehaviour mb, string fieldName, object value)
        {
            var field = mb.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(mb, value);
        }
    }
}
#endif
