#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArenaSetup
{
    /// <summary>
    /// Constrói a UI da cena de menu principal e liga todos os componentes.
    ///
    /// Menu: <c>Arena &gt; Setup Main Menu Scene</c>. Funciona na cena aberta; se a cena
    /// <c>menu_principal</c> ainda não existir, ela é criada. Ao final, salva a cena e a adiciona
    /// ao Build Settings.
    /// </summary>
    public static class ArenaMenuSceneSetup
    {
        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        private static readonly Color CardColor = new Color(0.12f, 0.13f, 0.18f, 0.95f);
        private static readonly Color AccentColor = new Color(0.20f, 0.45f, 0.85f, 1f);
        private static readonly Color MutedColor = new Color(0.16f, 0.17f, 0.22f, 1f);
        private static readonly Color TextColor = new Color(0.95f, 0.96f, 1f, 1f);

        [MenuItem("Arena/Setup Main Menu Scene", priority = 20)]
        public static void SetupMainMenuScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.isLoaded)
            {
                Debug.LogError("[Arena] Nenhuma cena aberta.");
                return;
            }

            // Se a cena atual não é a de menu e ela existe no projeto, abre a de menu.
            if (!string.Equals(scene.name, "MenuPrincipal", System.StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaEditorUtil.MenuScenePath) != null)
            {
                scene = EditorSceneManager.OpenScene(ArenaEditorUtil.MenuScenePath, OpenSceneMode.Single);
            }

            RemoveExistingMenu();

            GameObject root = new GameObject("MenuPrincipal");
            MenuManager manager = root.AddComponent<MenuManager>();

            Canvas canvas = ArenaEditorUtil.CreateCanvas("Canvas_Menu", root.transform, 0);
            ArenaEditorUtil.EnsureEventSystem();

            // Os paineis precisam ser FILHOS do Canvas: um painel irmao do Canvas (filho do
            // MenuPrincipal) nao e renderizado pela uGUI.
            BuildMainPanel(canvas.transform, canvas.transform, manager, out GameObject mainPanel, out TextMeshProUGUI message);
            GameObject difficultyPanel = BuildDifficultyPanel(canvas.transform, canvas.transform, manager);
            GameObject settingsPanel = BuildSettingsPanel(canvas.transform, canvas.transform, manager);

            manager.Configure(mainPanel, difficultyPanel, settingsPanel);
            ArenaEditorUtil.SetField(manager, "messageText", message);

            difficultyPanel.SetActive(false);
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ArenaEditorUtil.MenuScenePath);
            AddSceneToBuildSettings(ArenaEditorUtil.MenuScenePath);

            Debug.Log("<b>[Arena] Menu principal montado.</b> Cena salva em " + ArenaEditorUtil.MenuScenePath +
                      ". O cargo inclui a cena no Build Settings. Pressione Play para testar.");
        }

        private static void RemoveExistingMenu()
        {
            // O Canvas é irmão do MenuPrincipal (não filho), então os dois precisam ser apagados.
            // O EventSystem é preservado: criá-lo de novo a cada execução duplicaria módulos de input.
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "MenuPrincipal" || go.name == "Canvas_Menu")
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                // Execucoes antigas deixavam os paineis soltos na raiz da cena.
                if (go.GetComponent<Canvas>() == null && IsMenuArtifact(go.name))
                    Object.DestroyImmediate(go);
            }
        }

        private static bool IsMenuArtifact(string name) =>
            name == "Panel_MenuPrincipal" || name == "Panel_Dificuldade" ||
            name == "Panel_Configuracoes" || name == "SettingsConfirmModal" ||
            name == "Mensagem";

        #region Menu principal

        private static void BuildMainPanel(Transform root, Transform canvas, MenuManager manager, out GameObject panel,
            out TextMeshProUGUI message)
        {
            Image background = ArenaEditorUtil.CreatePanel(canvas, "Panel_MenuPrincipal", new Color(0.03f, 0.04f, 0.06f, 1f));
            ArenaEditorUtil.Stretch(background.rectTransform);
            panel = background.gameObject;

            // --- Coluna esquerda: ranking ---
            Image rankingCard = ArenaEditorUtil.CreatePanel(panel.transform, "Panel_Ranking", CardColor);
            ArenaEditorUtil.Place(rankingCard.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(48f, 0f), new Vector2(560f, 760f));

            TextMeshProUGUI rankingTitle = ArenaEditorUtil.CreateText(rankingCard.transform, "TituloRanking",
                "Ranking — Fácil", 34f, TextAlignmentOptions.Center, TextColor, bold: true);
            ArenaEditorUtil.Place(rankingTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(500f, 50f));

            Button facilTab = CreateTab(rankingCard.transform, "AbaFacil", "Fácil", new Vector2(-160f, -88f));
            Button medioTab = CreateTab(rankingCard.transform, "AbaMedio", "Médio", new Vector2(0f, -88f));
            Button dificilTab = CreateTab(rankingCard.transform, "AbaDificil", "Difícil", new Vector2(160f, -88f));

            Button[] tabs = { facilTab, medioTab, dificilTab };
            foreach (Button tab in tabs)
            {
                RectTransform rect = (RectTransform)tab.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }

            // Cabeçalho das colunas
            TextMeshProUGUI nameHeader = ArenaEditorUtil.CreateText(rankingCard.transform, "CabecalhoNome", "NOME",
                22f, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.55f), bold: true);
            ArenaEditorUtil.Place(nameHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -150f), new Vector2(300f, 32f));

            TextMeshProUGUI scoreHeader = ArenaEditorUtil.CreateText(rankingCard.transform, "CabecalhoPontos", "PONTOS",
                22f, TextAlignmentOptions.Right, new Color(1f, 1f, 1f, 0.55f), bold: true);
            ArenaEditorUtil.Place(scoreHeader.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -150f), new Vector2(220f, 32f));

            // 5 linhas x 2 colunas
            TMP_Text[] names = new TMP_Text[RankingService.MaximumEntries];
            TMP_Text[] scores = new TMP_Text[RankingService.MaximumEntries];

            for (int i = 0; i < RankingService.MaximumEntries; i++)
            {
                float y = -196f - i * 68f;

                Image row = ArenaEditorUtil.CreatePanel(rankingCard.transform, "Linha" + (i + 1),
                    i % 2 == 0 ? new Color(1f, 1f, 1f, 0.04f) : new Color(0f, 0f, 0f, 0f));
                ArenaEditorUtil.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, y), new Vector2(500f, 58f));

                TextMeshProUGUI nameText = ArenaEditorUtil.CreateText(row.transform, "Nome", (i + 1) + ". ---", 26f,
                    TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.35f));
                ArenaEditorUtil.Place(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(16f, 0f), new Vector2(300f, 40f));

                TextMeshProUGUI scoreText = ArenaEditorUtil.CreateText(row.transform, "Pontos", "0", 26f,
                    TextAlignmentOptions.Right, new Color(1f, 1f, 1f, 0.35f));
                ArenaEditorUtil.Place(scoreText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-16f, 0f), new Vector2(160f, 40f));

                names[i] = nameText;
                scores[i] = scoreText;
            }

            RankingDisplay ranking = rankingCard.gameObject.AddComponent<RankingDisplay>();
            ranking.Configure(facilTab, medioTab, dificilTab, rankingTitle, names, scores);

            // --- Coluna central: título + botões ---
            TextMeshProUGUI title = ArenaEditorUtil.CreateText(panel.transform, "TituloJogo", "ARENA JW",
                96f, TextAlignmentOptions.Center, TextColor, bold: true);
            ArenaEditorUtil.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 300f), new Vector2(900f, 120f));

            TextMeshProUGUI subtitle = ArenaEditorUtil.CreateText(panel.transform, "Subtitulo",
                "Sobreviva o máximo que puder", 28f, TextAlignmentOptions.Center,
                new Color(1f, 1f, 1f, 0.6f));
            ArenaEditorUtil.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 215f), new Vector2(900f, 44f));

            message = ArenaEditorUtil.CreateText(panel.transform, "Mensagem", string.Empty, 24f,
                TextAlignmentOptions.Center, new Color(1f, 0.75f, 0.3f));
            ArenaEditorUtil.Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -230f), new Vector2(900f, 60f));
            message.gameObject.SetActive(false);

            Button playButton = CreateMainButton(panel.transform, "BotaoJogar", "Jogar", 220f, AccentColor);
            Button settingsButton = CreateMainButton(panel.transform, "BotaoConfiguracoes", "Configurações", 110f, MutedColor);
            Button quitButton = CreateMainButton(panel.transform, "BotaoSair", "Sair", 0f, MutedColor);

            playButton.onClick.AddListener(manager.ShowDifficulty);
            settingsButton.onClick.AddListener(manager.ShowSettings);
            quitButton.onClick.AddListener(manager.QuitGame);

            // --- Coluna direita: créditos ---
            Image creditsCard = ArenaEditorUtil.CreatePanel(panel.transform, "Panel_Creditos", CardColor);
            ArenaEditorUtil.Place(creditsCard.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-48f, 0f), new Vector2(440f, 760f));

            TextMeshProUGUI creditsTitle = ArenaEditorUtil.CreateText(creditsCard.transform, "TituloCreditos",
                "CRÉDITOS", 30f, TextAlignmentOptions.Center, TextColor, bold: true);
            ArenaEditorUtil.Place(creditsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(380f, 44f));

            TextMeshProUGUI creditsBody = ArenaEditorUtil.CreateText(creditsCard.transform, "Creditos",
                "Desenvolvido nas aulas de Jogos Digitais.\n\n" +
                "Arte: modelo LEGO do tanque\n" +
                "Motor: Unity 6 · URP\n" +
                "Áudio, UI e programação:\nArena JW team\n\n" +
                "Obrigado por jogar!",
                24f, TextAlignmentOptions.Top, new Color(1f, 1f, 1f, 0.8f));
            creditsBody.rectTransform.anchorMin = new Vector2(0f, 0f);
            creditsBody.rectTransform.anchorMax = new Vector2(1f, 1f);
            creditsBody.rectTransform.offsetMin = new Vector2(28f, 28f);
            creditsBody.rectTransform.offsetMax = new Vector2(-28f, -90f);
        }

        private static Button CreateTab(Transform parent, string name, string label, Vector2 position)
        {
            Button button = ArenaEditorUtil.CreateButton(parent, name, label, new Vector2(150f, 56f), MutedColor);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(position.x, position.y + 24f);
            return button;
        }

        private static Button CreateMainButton(Transform parent, string name, string label, float offsetY, Color color)
        {
            Button button = ArenaEditorUtil.CreateButton(parent, name, label, new Vector2(420f, 76f), color);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, offsetY);

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.fontSize = 34f;

            return button;
        }

        #endregion

        #region Painel de dificuldade

        private static GameObject BuildDifficultyPanel(Transform root, Transform canvas, MenuManager manager)
        {
            Image dim = ArenaEditorUtil.CreatePanel(canvas, "Panel_Dificuldade", new Color(0f, 0f, 0f, 0.75f));
            ArenaEditorUtil.Stretch(dim.rectTransform);

            Image window = ArenaEditorUtil.CreatePanel(dim.transform, "Janela", PanelColor);
            ArenaEditorUtil.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820f, 700f));

            TextMeshProUGUI title = ArenaEditorUtil.CreateText(window.transform, "Titulo", "Selecione a dificuldade",
                48f, TextAlignmentOptions.Center, TextColor, bold: true);
            ArenaEditorUtil.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(740f, 64f));

            Button facil = CreateDifficultyButton(window.transform, "BotaoFacil", "Fácil", 200f);
            Button medio = CreateDifficultyButton(window.transform, "BotaoMedio", "Médio", 110f);
            Button dificil = CreateDifficultyButton(window.transform, "BotaoDificil", "Difícil", 20f);

            TextMeshProUGUI description = ArenaEditorUtil.CreateText(window.transform, "Descricao",
                "Escolha uma dificuldade.", 24f, TextAlignmentOptions.Center,
                new Color(1f, 1f, 1f, 0.75f));
            ArenaEditorUtil.Place(description.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 130f), new Vector2(740f, 80f));

            Button cancel = ArenaEditorUtil.CreateButton(window.transform, "BotaoCancelar", "Cancelar",
                new Vector2(300f, 64f), MutedColor);
            ArenaEditorUtil.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(300f, 64f));

            DifficultyPanel panel = dim.gameObject.AddComponent<DifficultyPanel>();
            panel.Configure(manager, facil, medio, dificil, cancel, description);

            // Painéis que cobrem a tela precisam do raycaster no próprio canvas (já criado).
            return dim.gameObject;
        }

        private static Button CreateDifficultyButton(Transform parent, string name, string label, float y)
        {
            Button button = ArenaEditorUtil.CreateButton(parent, name, label, new Vector2(600f, 76f), MutedColor);
            ArenaEditorUtil.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f - (200f - y)), new Vector2(600f, 76f));
            return button;
        }

        #endregion

        #region Painel de configurações

        private static GameObject BuildSettingsPanel(Transform root, Transform canvas, MenuManager manager)
        {
            Image dim = ArenaEditorUtil.CreatePanel(canvas, "Panel_Configuracoes", new Color(0f, 0f, 0f, 0.75f));
            ArenaEditorUtil.Stretch(dim.rectTransform);

            Image window = ArenaEditorUtil.CreatePanel(dim.transform, "Janela", PanelColor);
            ArenaEditorUtil.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 780f));

            TextMeshProUGUI title = ArenaEditorUtil.CreateText(window.transform, "Titulo", "Configurações",
                48f, TextAlignmentOptions.Center, TextColor, bold: true);
            ArenaEditorUtil.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(820f, 64f));

            Slider master = CreateSettingRow(window.transform, "VolumeGeral", "Volume Geral", 240f, 680f, out _);
            Slider sfx = CreateSettingRow(window.transform, "VolumeEfeitos", "Volume Efeitos", 330f, 680f, out _);
            Slider music = CreateSettingRow(window.transform, "VolumeMusica", "Volume Música", 420f, 680f, out _);

            TextMeshProUGUI resolutionLabel = CreateLabel(window.transform, "RotuloResolucao", "Resolução", 510f);
            TMP_Dropdown resolution = ArenaEditorUtil.CreateDropdown(window.transform, "Resolucao_Dropdown");
            ArenaEditorUtil.Place((RectTransform)resolution.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(150f, -510f), new Vector2(420f, 52f));

            TextMeshProUGUI displayLabel = CreateLabel(window.transform, "RotuloExibicao", "Exibição", 590f);
            TMP_Dropdown display = ArenaEditorUtil.CreateDropdown(window.transform, "Exibicao_Dropdown");
            ArenaEditorUtil.Place((RectTransform)display.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(150f, -590f), new Vector2(420f, 52f));

            TextMeshProUGUI status = ArenaEditorUtil.CreateText(window.transform, "Status", string.Empty, 22f,
                TextAlignmentOptions.Center, new Color(0.5f, 1f, 0.6f, 1f));
            ArenaEditorUtil.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 178f), new Vector2(820f, 34f));

            Button apply = ArenaEditorUtil.CreateButton(window.transform, "BotaoAplicar", "Aplicar",
                new Vector2(300f, 68f), AccentColor);
            ArenaEditorUtil.Place((RectTransform)apply.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-170f, 100f), new Vector2(300f, 68f));

            Button back = ArenaEditorUtil.CreateButton(window.transform, "BotaoVoltar", "Voltar",
                new Vector2(300f, 68f), MutedColor);
            ArenaEditorUtil.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(170f, 100f), new Vector2(300f, 68f));

            SettingsConfirmModal modal = BuildConfirmModal(canvas.transform, canvas);

            SettingsPanel panel = dim.gameObject.AddComponent<SettingsPanel>();
            panel.Configure(manager, modal, master, sfx, music, resolution, display, apply, back, status);

            return dim.gameObject;
        }

        private static Slider CreateSettingRow(Transform parent, string name, string label, float y, float width, out TMP_Text labelText)
        {
            labelText = CreateLabel(parent, "Rotulo" + name, label, y);

            Slider slider = ArenaEditorUtil.CreateSlider(parent, "Slider" + name);
            ArenaEditorUtil.Place((RectTransform)slider.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(150f, -y), new Vector2(420f, 40f));

            return slider;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string value, float y)
        {
            TextMeshProUGUI label = ArenaEditorUtil.CreateText(parent, name, value, 26f,
                TextAlignmentOptions.Left, TextColor);
            ArenaEditorUtil.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-230f, -y), new Vector2(260f, 44f));
            label.rectTransform.pivot = new Vector2(1f, 0.5f);
            return label;
        }

        private static SettingsConfirmModal BuildConfirmModal(Transform root, Transform canvas)
        {
            Image dim = ArenaEditorUtil.CreatePanel(canvas, "SettingsConfirmModal", new Color(0f, 0f, 0f, 0.6f));
            ArenaEditorUtil.Stretch(dim.rectTransform);
            dim.transform.SetAsLastSibling();

            Image window = ArenaEditorUtil.CreatePanel(dim.transform, "Janela", CardColor);
            ArenaEditorUtil.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(920f, 360f));

            TextMeshProUGUI message = ArenaEditorUtil.CreateText(window.transform, "Mensagem",
                SettingsConfirmModal.DefaultMessage, 26f, TextAlignmentOptions.Center, TextColor);
            ArenaEditorUtil.Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 56f), new Vector2(820f, 160f));

            Button yes = ArenaEditorUtil.CreateButton(window.transform, "BotaoSim", "Sim, voltar",
                new Vector2(340f, 68f), AccentColor);
            ArenaEditorUtil.Place((RectTransform)yes.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-190f, -110f), new Vector2(340f, 68f));

            Button no = ArenaEditorUtil.CreateButton(window.transform, "BotaoNao", "Não, permanecer",
                new Vector2(340f, 68f), MutedColor);
            ArenaEditorUtil.Place((RectTransform)no.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(190f, -110f), new Vector2(340f, 68f));

            SettingsConfirmModal modal = dim.gameObject.AddComponent<SettingsConfirmModal>();
            modal.Configure(dim.gameObject, message, yes, no);
            return modal;
        }

        #endregion

        #region Build Settings

        public static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (EditorBuildSettingsScene scene in scenes)
                if (scene.path == path) return;

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Arena] Cena adicionada ao Build Settings: " + path);
        }

        #endregion
    }
}
#endif
