using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida + ícone de estado acima do inimigo.
///
/// A UI é montada em código dentro de um Canvas em World Space filho do próprio inimigo,
/// então o prefab não precisa de hierarquia de UI e o componente funciona em qualquer nave.
/// Os ícones (Patrulha / Perseguição / Ataque) são opcionais: sem sprite, o campo mostra a cor
/// do estado (amarelo, laranja, vermelho).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Enemy/Enemy Health Bar")]
public sealed class EnemyHealthBar : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Vida life;
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image stateIcon;

    [Header("Ícones de estado (opcional)")]
    [SerializeField] private Sprite patrolIcon;
    [SerializeField] private Sprite pursueIcon;
    [SerializeField] private Sprite attackIcon;

    [Header("Cores")]
    [SerializeField] private Color fullColor = new Color(0.30f, 1f, 0.35f);
    [SerializeField] private Color emptyColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color patrolColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color pursueColor = new Color(1f, 0.55f, 0.1f);
    [SerializeField] private Color attackColor = new Color(1f, 0.15f, 0.15f);

    [Header("Posicionamento")]
    [Tooltip("Deslocamento acima do pivô do inimigo, em metros.")]
    [SerializeField] private float height = 3.2f;
    [SerializeField] private Vector2 size = new Vector2(1.6f, 0.28f);
    [Tooltip("Tamanho do ícone de estado, em metros.")]
    [SerializeField, Min(0.05f)] private float iconSize = 0.38f;
    [Tooltip("Deixa a barra sempre de frente para a câmera.")]
    [SerializeField] private bool billboard = true;

    private Transform target;
    private Canvas canvas;
    private float lastFill = -1f;
    private string lastState;

    private void Awake()
    {
        if (life == null) life = GetComponentInParent<Vida>();
        if (uiRoot == null) Build();
        if (life != null) life.AoAlterarVida += OnHealthChanged;
    }

    private void Start()
    {
        // No Start a Vida já rodou o próprio Awake e o valor inicial é confiável.
        if (life != null) OnHealthChanged(life.VidaAtual, life.VidaMaxima);
    }

    private void OnDestroy()
    {
        if (life != null) life.AoAlterarVida -= OnHealthChanged;
    }

    private Camera ResolveCamera()
    {
        if (target == null) target = Camera.main != null ? Camera.main.transform : null;
        return target != null ? target.GetComponent<Camera>() : Camera.main;
    }

    private void LateUpdate()
    {
        if (uiRoot == null) return;

        Transform anchor = transform;
        uiRoot.transform.position = anchor.position + Vector3.up * height;

        if (billboard)
        {
            Camera cam = ResolveCamera();
            if (cam != null) uiRoot.transform.rotation = cam.transform.rotation;
        }

        IEnemyStateProvider provider = GetComponent<IEnemyStateProvider>();
        if (provider != null && provider.StateName != lastState)
        {
            lastState = provider.StateName;
            ApplyStateVisual(provider.StateName);
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        if (fillImage == null) return;
        float fill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (Mathf.Approximately(fill, lastFill)) return;
        lastFill = fill;
        fillImage.fillAmount = fill;
        fillImage.color = Color.Lerp(emptyColor, fullColor, fill);
    }

    private void ApplyStateVisual(string stateName)
    {
        if (stateIcon == null) return;

        Sprite sprite = null;
        Color color = Color.white;

        switch (stateName)
        {
            case "Patrol":
                sprite = patrolIcon; color = patrolColor; break;
            case "Pursue":
                sprite = pursueIcon; color = pursueColor; break;
            case "Attack":
                sprite = attackIcon; color = attackColor; break;
            default:
                sprite = patrolIcon; color = patrolColor; break;
        }

        stateIcon.sprite = sprite;
        stateIcon.color = sprite != null ? color : new Color(color.r, color.g, color.b, 0.9f);
        stateIcon.enabled = true;
    }

    /// <summary>Cria o Canvas World Space com a barra e o ícone. Chamado no Awake quando não há UI.</summary>
    private void Build()
    {
        uiRoot = new GameObject("UI_Inimigo", typeof(RectTransform));
        uiRoot.transform.SetParent(transform, false);

        canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = (RectTransform)uiRoot.transform;
        canvasRect.anchorMin = canvasRect.anchorMax = canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.sizeDelta = new Vector2(100f, 20f);
        canvasRect.localScale = Vector3.one * 0.01f;
        canvasRect.localPosition = Vector3.up * height;

        // Fundo da barra
        GameObject background = new GameObject("Fundo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        background.transform.SetParent(uiRoot.transform, false);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.sizeDelta = new Vector2(100f, 14f);
        backgroundRect.anchoredPosition = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.65f);
        backgroundImage.raycastTarget = false;

        // Preenchimento
        GameObject fill = new GameObject("Vida", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(background.transform, false);
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillImage = fill.GetComponent<Image>();
        fillImage.color = fullColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.raycastTarget = false;

        // Ícone de estado, à esquerda da barra
        GameObject icon = new GameObject("Estado", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(uiRoot.transform, false);
        RectTransform iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(1f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize * 100f, iconSize * 100f);
        iconRect.anchoredPosition = new Vector2(-8f, 0f);
        stateIcon = icon.GetComponent<Image>();
        stateIcon.raycastTarget = false;

        // Escala a barra conforme o tamanho pedido (a UI é construída em 100 x 20 "unidades").
        canvasRect.localScale = new Vector3(size.x / 100f, size.x / 100f, size.x / 100f);
        ApplyStateVisual("Patrol");
    }

    /// <summary>Permite trocar os ícones em runtime (ex.: carregados de Resources).</summary>
    public void SetIcons(Sprite patrol, Sprite pursue, Sprite attack)
    {
        patrolIcon = patrol;
        pursueIcon = pursue;
        attackIcon = attack;
        lastState = null;
    }
}
