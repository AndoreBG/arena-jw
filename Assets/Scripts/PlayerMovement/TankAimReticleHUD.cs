using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de mira com 2 retículos:
/// 1) Retículo central da câmera (fixo no ponto central da visão da câmera);
/// 2) Retículo da direção do canhão do tanque (síncrono com o giro e elevação do Trabuco,
///    projetando um ponto a uma distância fixa à frente do canhão).
/// </summary>
[DefaultExecutionOrder(110)] // Executa logo após TankTurretAim (100) para sincronia total no mesmo frame.
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Aim Reticle HUD")]
public sealed class TankAimReticleHUD : MonoBehaviour
{
    [Header("Referências do Tanque e Câmera")]
    [Tooltip("Componente TankTurretAim do tanque. Se vazio, busca no próprio GameObject.")]
    [SerializeField] private TankTurretAim turretAim;

    [Tooltip("Transform cuja frente (+Z) representa a direção do canhão (Trabuco).")]
    [SerializeField] private Transform barrelTransform;

    [Tooltip("Transform de origem na ponta do canhão (ex.: FX_Tiro). Se vazio, usa a ponta do Trabuco.")]
    [SerializeField] private Transform barrelOrigin;

    [Tooltip("Câmera usada na mira. Se vazio, usa a câmera do TankTurretAim ou Camera.main.")]
    [SerializeField] private Camera aimCamera;

    [Header("Projeção da Direção do Canhão")]
    [Tooltip("Distância fixa à frente do canhão usada para projetar o retículo do canhão na tela.")]
    [SerializeField, Min(1f)] private float aimDistance = 50f;

    [Header("UI dos Retículos")]
    [Tooltip("Canvas onde os retículos estão inseridos.")]
    [SerializeField] private Canvas hudCanvas;

    [Tooltip("Retículo 1: ponto central da câmera.")]
    [SerializeField] private RectTransform cameraReticle;

    [Tooltip("Retículo 2: posição projetada da direção do canhão do tanque.")]
    [SerializeField] private RectTransform barrelReticle;

    [Header("Aparência Padrão dos Retículos")]
    [SerializeField] private Color cameraReticleColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color barrelReticleColor = new Color(1f, 0.55f, 0.1f, 0.95f);
    [SerializeField] private Vector2 cameraReticleSize = new Vector2(36f, 36f);
    [SerializeField] private Vector2 barrelReticleSize = new Vector2(44f, 44f);

    private RectTransform canvasRectTransform;
    private Graphic barrelReticleGraphic;

    private static Sprite cachedCameraReticleSprite;
    private static Sprite cachedBarrelReticleSprite;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureHUDAndSprites();
    }

    private void LateUpdate()
    {
        if (aimCamera == null)
            aimCamera = turretAim != null ? turretAim.AimCamera : Camera.main;

        if (aimCamera == null || canvasRectTransform == null)
            return;

        UpdateCameraCenterReticle();
        UpdateBarrelDirectionReticle();
    }

    /// <summary>
    /// Mantém o 1º retículo exatamente no ponto central da câmera.
    /// </summary>
    private void UpdateCameraCenterReticle()
    {
        if (cameraReticle == null)
            return;

        Vector3 screenCenter = aimCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
        SetReticleScreenPosition(cameraReticle, new Vector2(screenCenter.x, screenCenter.y));
    }

    /// <summary>
    /// Posiciona o 2º retículo de forma síncrona com a direção atual do canhão do tanque,
    /// projetando na tela um ponto a uma distância fixa na linha de direção do canhão.
    /// </summary>
    private void UpdateBarrelDirectionReticle()
    {
        if (barrelReticle == null)
            return;

        Vector3 origin = GetBarrelOriginPosition();
        Vector3 direction = GetBarrelDirection();
        Vector3 worldAimPoint = origin + direction * aimDistance;

        Vector3 screenPoint = aimCamera.WorldToScreenPoint(worldAimPoint);
        bool isInFrontOfCamera = screenPoint.z > 0f;

        if (barrelReticleGraphic != null && barrelReticleGraphic.enabled != isInFrontOfCamera)
            barrelReticleGraphic.enabled = isInFrontOfCamera;

        if (!isInFrontOfCamera)
            return;

        SetReticleScreenPosition(barrelReticle, new Vector2(screenPoint.x, screenPoint.y));
    }

    private void SetReticleScreenPosition(RectTransform reticle, Vector2 screenPosition)
    {
        Camera uiCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (hudCanvas.worldCamera != null ? hudCanvas.worldCamera : aimCamera)
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform,
                screenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            reticle.anchoredPosition = localPoint;
        }
    }

    private Vector3 GetBarrelOriginPosition()
    {
        if (barrelOrigin != null)
            return barrelOrigin.position;

        if (turretAim != null)
            return turretAim.BarrelMuzzlePosition;

        if (barrelTransform != null)
            return barrelTransform.position;

        return transform.position;
    }

    private Vector3 GetBarrelDirection()
    {
        if (barrelTransform != null && barrelTransform.forward.sqrMagnitude > 0.0001f)
            return barrelTransform.forward.normalized;

        if (turretAim != null)
            return turretAim.BarrelDirection.normalized;

        return transform.forward;
    }

    private void ResolveReferences()
    {
        if (turretAim == null)
            turretAim = GetComponent<TankTurretAim>();

        if (turretAim == null)
            turretAim = FindFirstObjectByType<TankTurretAim>();

        if (barrelTransform == null && turretAim != null)
            barrelTransform = turretAim.AimReference;

        if (barrelTransform == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name.Equals("Trabuco", System.StringComparison.OrdinalIgnoreCase))
                {
                    barrelTransform = child;
                    break;
                }
            }
        }

        if (barrelOrigin == null && barrelTransform != null)
        {
            foreach (Transform child in barrelTransform)
            {
                if (child.name.Equals("FX_Tiro", System.StringComparison.OrdinalIgnoreCase))
                {
                    barrelOrigin = child;
                    break;
                }
            }
        }

        if (aimCamera == null)
            aimCamera = turretAim != null ? turretAim.AimCamera : Camera.main;
    }

    private void EnsureHUDAndSprites()
    {
        if (hudCanvas == null)
        {
            if (cameraReticle != null)
                hudCanvas = cameraReticle.GetComponentInParent<Canvas>();
            else if (barrelReticle != null)
                hudCanvas = barrelReticle.GetComponentInParent<Canvas>();
        }

        if (hudCanvas == null)
        {
            GameObject canvasGO = new GameObject("HUD_Mira");
            hudCanvas = canvasGO.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        canvasRectTransform = hudCanvas.GetComponent<RectTransform>();

        if (cameraReticle == null)
            cameraReticle = CreateReticleElement("ReticuloCamera", hudCanvas.transform, cameraReticleSize, cameraReticleColor);

        if (barrelReticle == null)
            barrelReticle = CreateReticleElement("ReticuloCanhao", hudCanvas.transform, barrelReticleSize, barrelReticleColor);

        ConfigureReticleVisual(cameraReticle, GetCameraReticleSprite(), cameraReticleSize, cameraReticleColor);
        ConfigureReticleVisual(barrelReticle, GetBarrelReticleSprite(), barrelReticleSize, barrelReticleColor);

        barrelReticleGraphic = barrelReticle.GetComponent<Graphic>();
    }

    private static RectTransform CreateReticleElement(string name, Transform parent, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        return rt;
    }

    private static void ConfigureReticleVisual(RectTransform reticle, Sprite fallbackSprite, Vector2 defaultSize, Color defaultColor)
    {
        if (reticle == null)
            return;

        reticle.anchorMin = new Vector2(0.5f, 0.5f);
        reticle.anchorMax = new Vector2(0.5f, 0.5f);
        reticle.pivot = new Vector2(0.5f, 0.5f);

        if (reticle.sizeDelta.sqrMagnitude < 1f)
            reticle.sizeDelta = defaultSize;

        Image img = reticle.GetComponent<Image>();
        if (img == null)
            img = reticle.gameObject.AddComponent<Image>();

        if (img.sprite == null)
            img.sprite = fallbackSprite;

        img.raycastTarget = false;
    }

    /// <summary>
    /// Cria o sprite procedural do retículo central da câmera (cruz vazada com ponto central).
    /// </summary>
    private static Sprite GetCameraReticleSprite()
    {
        if (cachedCameraReticleSprite != null)
            return cachedCameraReticleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Sprite_ReticuloCamera",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center);
                float dy = Mathf.Abs(y - center);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = 0f;

                // Ponto central
                if (dist <= 2.2f)
                    alpha = Mathf.Clamp01(2.5f - dist);

                // Quatro hastes da cruz central
                bool horizontalArm = dy <= 1.2f && dx >= 7f && dx <= 18f;
                bool verticalArm = dx <= 1.2f && dy >= 7f && dy <= 18f;
                if (horizontalArm || verticalArm)
                    alpha = Mathf.Max(alpha, 0.9f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCameraReticleSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);

        return cachedCameraReticleSprite;
    }

    /// <summary>
    /// Cria o sprite procedural do retículo do canhão (anel circular com marcadores internos).
    /// </summary>
    private static Sprite GetBarrelReticleSprite()
    {
        if (cachedBarrelReticleSprite != null)
            return cachedBarrelReticleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Sprite_ReticuloCanhao",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center);
                float dy = Mathf.Abs(y - center);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = 0f;

                // Anel circular principal
                float ringDist = Mathf.Abs(dist - 22f);
                if (ringDist <= 2.0f)
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(2.2f - ringDist));

                // Marcadores cardeais apontando para o centro
                bool tickH = dy <= 1.3f && dx >= 14f && dx <= 26f;
                bool tickV = dx <= 1.3f && dy >= 14f && dy <= 26f;
                if (tickH || tickV)
                    alpha = Mathf.Max(alpha, 0.95f);

                // Pequeno ponto central
                if (dist <= 1.8f)
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(2.0f - dist));

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedBarrelReticleSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);

        return cachedBarrelReticleSprite;
    }

    private void OnValidate()
    {
        aimDistance = Mathf.Max(1f, aimDistance);
    }
}
