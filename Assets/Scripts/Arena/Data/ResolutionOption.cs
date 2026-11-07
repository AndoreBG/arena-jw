using UnityEngine;

/// <summary>Uma entrada do dropdown de resolução. Os assets são gerados pelo Editor script.</summary>
[CreateAssetMenu(menuName = "Arena/Resolution Option", fileName = "Resolucao")]
public sealed class ResolutionOption : ScriptableObject
{
    [SerializeField, Min(320)] private int width = 1920;
    [SerializeField, Min(240)] private int height = 1080;
    [SerializeField] private string label = "1920 × 1080";

    public int Width => width;
    public int Height => height;
    public string Label => string.IsNullOrWhiteSpace(label) ? width + " × " + height : label;

    public Resolution ToResolution() => new Resolution { width = width, height = height };

#if UNITY_EDITOR
    public void EditorConfigure(int w, int h)
    {
        width = Mathf.Max(320, w);
        height = Mathf.Max(240, h);
        label = width + " × " + height;
    }
#endif
}
