using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ranking do menu principal: três abas (Fácil / Médio / Difícil) e, em cada uma, cinco linhas
/// com Nome | Pontuação. As linhas vêm prontas da cena (criadas pelo Editor script) e os dados
/// vêm do <see cref="RankingService"/>.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Menu/Ranking Display")]
public sealed class RankingDisplay : MonoBehaviour
{
    [Header("Abas")]
    [SerializeField] private Button facilTab;
    [SerializeField] private Button medioTab;
    [SerializeField] private Button dificilTab;
    [SerializeField] private TMP_Text selectedTitle;

    [Header("Linhas (5)")]
    [SerializeField] private TMP_Text[] nameTexts = new TMP_Text[RankingService.MaximumEntries];
    [SerializeField] private TMP_Text[] scoreTexts = new TMP_Text[RankingService.MaximumEntries];

    [Header("Cores das abas")]
    [SerializeField] private Color activeTabColor = new Color(0.20f, 0.45f, 0.85f);
    [SerializeField] private Color inactiveTabColor = new Color(0.16f, 0.17f, 0.22f);

    private ArenaDifficulty selected = ArenaDifficulty.Facil;

    private void Awake()
    {
        Wire(facilTab, ArenaDifficulty.Facil);
        Wire(medioTab, ArenaDifficulty.Medio);
        Wire(dificilTab, ArenaDifficulty.Dificil);
    }

    private void OnEnable() => Refresh();

    private void Wire(Button button, ArenaDifficulty difficulty)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => Select(difficulty));
    }

    public void Select(ArenaDifficulty difficulty)
    {
        selected = difficulty;
        Refresh();
    }

    /// <summary>Recarrega o ranking do disco e redesenha as linhas.</summary>
    public void Refresh()
    {
        UpdateTabColors();
        if (selectedTitle != null) selectedTitle.text = "Ranking — " + DisplayName(selected);

        List<RankingEntry> entries;
        string error;
        bool ok = RankingService.TryGetEntries(selected, out entries, out error);

        for (int i = 0; i < RankingService.MaximumEntries; i++)
        {
            TMP_Text nameText = nameTexts != null && i < nameTexts.Length ? nameTexts[i] : null;
            TMP_Text scoreText = scoreTexts != null && i < scoreTexts.Length ? scoreTexts[i] : null;

            bool hasEntry = ok && entries != null && i < entries.Count;

            if (nameText != null)
            {
                nameText.text = hasEntry ? (i + 1) + ". " + entries[i].nickname : (i + 1) + ". ---";
                nameText.color = hasEntry ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }

            if (scoreText != null)
            {
                scoreText.text = hasEntry ? entries[i].score.ToString() : "0";
                scoreText.color = nameText != null ? nameText.color : Color.white;
            }
        }

        if (!ok && !string.IsNullOrEmpty(error) && nameTexts != null && nameTexts.Length > 0 && nameTexts[0] != null)
            nameTexts[0].text = "Ranking indisponível";
    }

    private void UpdateTabColors()
    {
        SetTabColor(facilTab, ArenaDifficulty.Facil);
        SetTabColor(medioTab, ArenaDifficulty.Medio);
        SetTabColor(dificilTab, ArenaDifficulty.Dificil);
    }

    private void SetTabColor(Button button, ArenaDifficulty difficulty)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = difficulty == selected ? activeTabColor : inactiveTabColor;
    }

    public static string DisplayName(ArenaDifficulty difficulty) => difficulty switch
    {
        ArenaDifficulty.Medio => "Médio",
        ArenaDifficulty.Dificil => "Difícil",
        _ => "Fácil"
    };

    /// <summary>Ligação usada pelo Editor script.</summary>
    public void Configure(Button facil, Button medio, Button dificil, TMP_Text title,
        TMP_Text[] names, TMP_Text[] scores)
    {
        facilTab = facil;
        medioTab = medio;
        dificilTab = dificil;
        selectedTitle = title;
        nameTexts = names;
        scoreTexts = scores;
    }
}
