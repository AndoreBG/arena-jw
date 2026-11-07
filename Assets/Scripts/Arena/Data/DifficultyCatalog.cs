using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo de dificuldades. Evita que cada componente da cena precise apontar para um asset
/// diferente: o painel de dificuldade, o spawner e o HUD consultam esta lista.
/// O asset gerado fica em <c>Assets/ScriptableObjects/DifficultyCatalog.asset</c>.
/// </summary>
[CreateAssetMenu(menuName = "Arena/Difficulty Catalog", fileName = "DifficultyCatalog")]
public sealed class DifficultyCatalog : ScriptableObject
{
    [SerializeField] private List<DifficultyConfig> difficulties = new List<DifficultyConfig>();

    public IReadOnlyList<DifficultyConfig> Difficulties => difficulties;

    public DifficultyConfig Get(ArenaDifficulty difficulty)
    {
        if (difficulties == null) return null;
        for (int i = 0; i < difficulties.Count; i++)
            if (difficulties[i] != null && difficulties[i].Difficulty == difficulty) return difficulties[i];
        return null;
    }

#if UNITY_EDITOR
    public void EditorConfigure(List<DifficultyConfig> values) => difficulties = values;
#endif
}
