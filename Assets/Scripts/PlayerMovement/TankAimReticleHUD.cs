using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de mira com dois retículos que JÁ EXISTEM na cena (ex.: HUD_Mira/ReticuloCamera e
/// HUD_Mira/ReticuloCanhao):
///
/// 1) Retículo central da câmera, fixo no centro do viewport;
/// 2) Retículo da direção do canhão, obtido projetando na tela um ponto à frente do Trabuco.
///
/// Este script NÃO cria canvas, não cria GameObjects de retículo e não gera sprites: o desenho é
/// seu, colocado no Source Image de cada objeto. Ele só cuida de três coisas:
///   - posicionar cada retículo, somando um offset em pixels do canvas (posição central);
///   - esconder o retículo do canhão quando a mira está atrás da câmera;
///   - animar a recarga do retículo do canhão depois de cada disparo, usando o Image.type =
///     Filled do próprio retículo (o clássico "miolo" de cooldown de habilidade).
/// </summary>
[DefaultExecutionOrder(110)] // Executa logo após TankTurretAim (100) para sincronia total no mesmo frame.
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Tank/Tank Aim Reticle HUD")]
public sealed class TankAimReticleHUD : MonoBehaviour
{
    /// <summary>Como o preenchimento do retículo evolui durante a recarga.</summary>
    public enum ReloadSweep
    {
        /// <summary>Começa vazio e enche até o retículo completo: cheio = pronto (padrão de MOBA).</summary>
        Recharges,
        /// <summary>Começa completo e esvazia; o retículo some e volta por cima no fim do cooldown.</summary>
        Depletes
    }

    /// <summary>Método de preenchimento aplicado ao Image do retículo.</summary>
    public enum ReloadFillMethod
    {
        Radial360,
        Radial180,
        Radial90,
        Horizontal,
        Vertical
    }

    [Header("Referências do Tanque e Câmera")]
    [Tooltip("Componente TankTurretAim do tanque. Se vazio, busca no próprio GameObject.")]
    [SerializeField] private TankTurretAim turretAim;

    [Tooltip("Transform cuja frente (+Z) representa a direção do canhão (ex.: Trabuco).")]
    [SerializeField] private Transform barrelTransform;

    [Tooltip("Transform de origem na ponta do canhão (ex.: FX_Tiro). Se vazio, usa a ponta do Trabuco.")]
    [SerializeField] private Transform barrelOrigin;

    [Tooltip("Câmera usada na mira. Se vazio, usa a câmera do TankTurretAim ou Camera.main.")]
    [SerializeField] private Camera aimCamera;

    [Header("Projeção da Direção do Canhão")]
    [Tooltip("Distância fixa à frente do canhão usada para projetar o retículo do canhão na tela.")]
    [SerializeField, Min(1f)] private float aimDistance = 50f;

    [Header("Retículos (criados por você, com Source Image próprio)")]
    [Tooltip("Canvas que contém os retículos. Se vazio, é deduzido do pai dos retículos.")]
    [SerializeField] private Canvas hudCanvas;

    [Tooltip("Retículo 1: ponto central da câmera.")]
    [SerializeField] private RectTransform cameraReticle;

    [Tooltip("Retículo 2: posição projetada da direção do canhão do tanque.")]
    [SerializeField] private RectTransform barrelReticle;

    [Header("Offset dos Retículos (pixels do canvas)")]
    [Tooltip("Desloca o centro do retículo da câmera. X para a direita, Y para cima. Não muda a trajetória do tiro.")]
    [SerializeField] private Vector2 cameraReticleOffset;

    [Tooltip("Desloca o centro do retículo do canhão. X para a direita, Y para cima. Não muda a trajetória do tiro.")]
    [SerializeField] private Vector2 barrelReticleOffset;

    [Header("Recarga do Retículo do Canhão")]
    [SerializeField, Tooltip("Liga o preenchimento do Image do retículo do canhão durante a recarga.")]
    private bool animateBarrelReload = true;

    [Tooltip("Atirador que fornece o tempo de recarga. Se vazio, busca no próprio GameObject. Sem atirador, " +
             "use PlayReload(duration) de onde quiser.")]
    [SerializeField] private TankPlayerInput shooter;

    [SerializeField] private ReloadFillMethod fillMethod = ReloadFillMethod.Radial360;

    [Tooltip("De onde o preenchimento parte. Radial 360: 0 = Right, 1 = Top, 2 = Left, 3 = Bottom. " +
             "Horizontal/Vertical: 0 = Left/Bottom, 1 = Right/Top.")]
    [SerializeField, Range(0, 3)] private int fillOrigin = 1;

    [SerializeField] private bool fillClockwise = true;

    [SerializeField] private ReloadSweep reloadSweep = ReloadSweep.Recharges;

    [Tooltip("Duração mínima da animação. Evita o retículo piscando quando o fireRate é muito baixo ou zero.")]
    [SerializeField, Min(0.01f)] private float minReloadDuration = 0.15f;

    private RectTransform canvasRectTransform;
    private Graphic barrelReticleGraphic;
    private Image barrelReticleImage;
    private Image.Type barrelImageOriginalType = Image.Type.Simple;
    private bool barrelImageWasRetyped;

    private int observedShotsFired = -1;
    private float reloadStartTime;
    private float reloadDuration;
    private bool reloadActive;
    private bool missingReferencesReported;
    private int shooterResolveAttempts;
    private const int maxShooterResolveAttempts = 60;

    // Cache do último fill aplicado, para não sujar a UI do Canvas sem necessidade.
    private float lastFillAmount = -1f;

    #region API pública

    /// <summary>Offset do retículo central, em pixels do canvas.</summary>
    public Vector2 CameraReticleOffset
    {
        get => cameraReticleOffset;
        set => cameraReticleOffset = value;
    }

    /// <summary>Offset do retículo do canhão, em pixels do canvas.</summary>
    public Vector2 BarrelReticleOffset
    {
        get => barrelReticleOffset;
        set => barrelReticleOffset = value;
    }

    /// <summary>Soma um deslocamento momentâneo aos dois retículos (ex.: chute de recuo).</summary>
    public void AddReticleOffset(Vector2 pixels)
    {
        cameraReticleOffset += pixels;
        barrelReticleOffset += pixels;
    }

    /// <summary>Progresso atual da recarga exibido no retículo: 0 (disparou agora) a 1 (pronto).</summary>
    public float ReloadProgress => reloadActive
        ? Mathf.Clamp01((Time.time - reloadStartTime) / Mathf.Max(0.0001f, reloadDuration))
        : 1f;

    /// <summary>True enquanto a animação de recarga está rolando.</summary>
    public bool IsReloading => reloadActive;

    /// <summary>Retículo do canhão, para quem quiser ler/ajustar o desenho no runtime.</summary>
    public RectTransform BarrelReticle => barrelReticle;

    /// <summary>Retículo central, idem.</summary>
    public RectTransform CameraReticle => cameraReticle;

    /// <summary>
    /// Inicia a animação de recarga manualmente (usado quando não há TankPlayerInput vinculado,
    /// por exemplo num retículo controlado pela arma de um NPC).
    /// </summary>
    public void PlayReload(float duration)
    {
        reloadDuration = Mathf.Max(minReloadDuration, duration);
        reloadStartTime = Time.time;
        reloadActive = true;
    }

    /// <summary>Liga/desliga a animação de recarga, restaurando o Image para o tipo original.</summary>
    public void SetReloadAnimated(bool enabled)
    {
        if (animateBarrelReload == enabled)
            return;

        animateBarrelReload = enabled;
        ApplyBarrelImageConfiguration();
    }

    #endregion

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();

        if (!ValidateSetup())
            return;

        CacheBarrelGraphic();
        ApplyBarrelImageConfiguration();
        SyncReloadWithShooter(true);
        WarnIfAnchorsAreNotCentered();
    }

    /// <summary>
    /// O posicionamento escreve em anchoredPosition tratando (0,0) como o centro da tela, o que
    /// só vale com anchor e pivot em 0.5/0.5. O script não corrige a hierarquia do usuário,
    /// apenas avisa — assim o offset não vira um deslocamento que depende da resolução.
    /// </summary>
    private void WarnIfAnchorsAreNotCentered()
    {
        WarnIfNotCentered(cameraReticle, "Camera Reticle");
        WarnIfNotCentered(barrelReticle, "Barrel Reticle");
    }

    private void WarnIfNotCentered(RectTransform reticle, string fieldName)
    {
        if (reticle == null)
            return;

        bool anchorsOk = reticle.anchorMin == new Vector2(0.5f, 0.5f)
            && reticle.anchorMax == new Vector2(0.5f, 0.5f)
            && reticle.pivot == new Vector2(0.5f, 0.5f);

        if (!anchorsOk)
        {
            Debug.LogWarning($"[{nameof(TankAimReticleHUD)}] '{reticle.name}' ({fieldName}) deveria ter " +
                             "Anchor Min/Max e Pivot em 0.5/0.5: o script posiciona por anchoredPosition " +
                             "a partir do centro, e o offset deixaria de ser em pixels do centro.", this);
        }
    }

    private void OnEnable()
    {
        if (canvasRectTransform != null)
            ApplyBarrelImageConfiguration();
    }

    private void LateUpdate()
    {
        if (aimCamera == null || canvasRectTransform == null)
            return;

        if (cameraReticle != null)
            UpdateCameraCenterReticle();

        if (barrelReticle != null)
            UpdateBarrelDirectionReticle();

        UpdateBarrelReload();
    }

    #region Posicionamento

    /// <summary>
    /// Mantém o 1º retículo no ponto central da câmera, mais o offset configurado.
    /// </summary>
    private void UpdateCameraCenterReticle()
    {
        Vector3 screenCenter = aimCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
        SetReticleScreenPosition(cameraReticle, new Vector2(screenCenter.x, screenCenter.y), cameraReticleOffset);
    }

    /// <summary>
    /// Posiciona o 2º retículo de forma síncrona com a direção atual do canhão, projetando na
    /// tela um ponto a uma distância fixa na linha de direção, mais o offset configurado.
    /// </summary>
    private void UpdateBarrelDirectionReticle()
    {
        Vector3 origin = GetBarrelOriginPosition();
        Vector3 direction = GetBarrelDirection();
        Vector3 worldAimPoint = origin + direction * aimDistance;

        Vector3 screenPoint = aimCamera.WorldToScreenPoint(worldAimPoint);
        bool isInFrontOfCamera = screenPoint.z > 0f;

        if (barrelReticleGraphic != null && barrelReticleGraphic.enabled != isInFrontOfCamera)
            barrelReticleGraphic.enabled = isInFrontOfCamera;

        if (!isInFrontOfCamera)
            return;

        SetReticleScreenPosition(barrelReticle, new Vector2(screenPoint.x, screenPoint.y), barrelReticleOffset);
    }

    private void SetReticleScreenPosition(RectTransform reticle, Vector2 screenPosition, Vector2 offset)
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
            // O offset é somado já no espaço do canvas, então continua em "pixels de referência"
            // mesmo com CanvasScaler em Scale With Screen Size.
            reticle.anchoredPosition = localPoint + offset;
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

    #endregion

    #region Recarga

    private void UpdateBarrelReload()
    {
        RetryResolveShooter();
        SyncReloadWithShooter(false);

        float progress = ReloadProgress;

        if (reloadActive && progress >= 1f)
            reloadActive = false;

        // Pronto = retículo inteiro. Durante a recarga, enche (Recharges) ou esvazia (Depletes).
        float fill = reloadActive
            ? (reloadSweep == ReloadSweep.Recharges ? progress : 1f - progress)
            : 1f;

        ApplyBarrelFill(fill);
    }

    /// <summary>
    /// Observa o contador de disparos do atirador: quando ele aumenta, a recarga recomeça, usando
    /// o tempo de cooldown já decorrido como origem da animação. Contar disparos (em vez de só ler
    /// o relógio) é o que faz a animação não rodar quando o tiro foi recusado por uma pool lotada,
    /// e ainda assim funciona com fireRate zero.
    /// </summary>
    private void SyncReloadWithShooter(bool initialSync)
    {
        if (shooter == null)
            return;

        int currentShots = shooter.ShotsFired;

        if (observedShotsFired < 0)
        {
            observedShotsFired = currentShots;

            // Começou o jogo no meio de um cooldown (retículo reativado, pausa, etc.).
            if (!initialSync)
                return;

            if (!shooter.IsReady)
                StartReload(shooter.FireRate, shooter.CooldownElapsed);

            return;
        }

        if (currentShots == observedShotsFired)
            return;

        observedShotsFired = currentShots;

        // O ponto de partida é o estado real do cooldown, e não "agora": assim a animação casa com
        // o disparo mesmo quando o HUD percebe o tiro um frame depois.
        float elapsed = shooter.CooldownElapsed;

        // Se o retículo estava desligado no momento do tiro (canvas oculto, HUD desabilitado) e o
        // cooldown já passou, encher o retículo de 0 agora seria um atraso inventado.
        if (elapsed >= Mathf.Max(minReloadDuration, shooter.FireRate))
        {
            reloadActive = false;
            return;
        }

        StartReload(shooter.FireRate, elapsed);
    }

    /// <summary>
    /// Se o HUD estiver num Canvas que deu Awake antes do do tanque (ou se o atirador for criado
    /// em runtime), o GetComponent do Awake não encontra nada. Insisto por ~1s e desisto depois.
    /// </summary>
    private void RetryResolveShooter()
    {
        if (shooter != null || shooterResolveAttempts >= maxShooterResolveAttempts)
            return;

        shooterResolveAttempts++;

        shooter = GetComponent<TankPlayerInput>();

        if (shooter == null)
            shooter = GetComponentInParent<TankPlayerInput>();

        if (shooter == null)
            shooter = FindFirstObjectByType<TankPlayerInput>();

        if (shooter != null)
            SyncReloadWithShooter(true);
    }

    private void StartReload(float cooldown, float alreadyElapsedFrom)
    {
        reloadDuration = Mathf.Max(minReloadDuration, cooldown);
        reloadStartTime = Time.time - Mathf.Clamp(alreadyElapsedFrom, 0f, reloadDuration);
        reloadActive = true;
    }

    private void ApplyBarrelFill(float amount)
    {
        if (barrelReticleImage == null || !animateBarrelReload)
            return;

        amount = Mathf.Clamp01(amount);

        if (Mathf.Approximately(lastFillAmount, amount))
            return;

        lastFillAmount = amount;
        barrelReticleImage.fillAmount = amount;
    }

    #endregion

    #region Configuração do Image e referências

    private void CacheBarrelGraphic()
    {
        if (barrelReticle == null)
            return;

        barrelReticleGraphic = barrelReticle.GetComponent<Graphic>();
        barrelReticleImage = barrelReticle.GetComponent<Image>();

        if (animateBarrelReload && barrelReticleImage == null)
        {
            Debug.LogWarning($"[{nameof(TankAimReticleHUD)}] '{barrelReticle.name}' não tem Image " +
                             "(é RawImage ou outro Graphic?) — a animação de recarga foi desligada " +
                             "para esse retículo.", this);
            animateBarrelReload = false;
        }
    }

    /// <summary>
    /// Prepara o Image do retículo do canhão para o preenchimento. O desenho (Source Image), o
    /// tamanho e a cor continuam sob seu controle no Inspector.
    /// </summary>
    private void ApplyBarrelImageConfiguration()
    {
        if (barrelReticleImage == null)
            return;

        if (!animateBarrelReload)
        {
            if (barrelImageWasRetyped)
            {
                barrelReticleImage.type = barrelImageOriginalType;
                barrelImageWasRetyped = false;
            }

            lastFillAmount = 1f;
            barrelReticleImage.fillAmount = 1f;
            return;
        }

        if (!barrelImageWasRetyped)
        {
            barrelImageOriginalType = barrelReticleImage.type;
            barrelImageWasRetyped = true;
        }

        barrelReticleImage.type = Image.Type.Filled;
        barrelReticleImage.fillMethod = MapFillMethod(fillMethod);
        barrelReticleImage.fillOrigin = Mathf.Clamp(fillOrigin, 0, 3);
        barrelReticleImage.fillClockwise = fillClockwise;
        lastFillAmount = -1f;
        ApplyBarrelFill(ReloadProgress);
    }

    private static Image.FillMethod MapFillMethod(ReloadFillMethod method)
    {
        switch (method)
        {
            case ReloadFillMethod.Radial180: return Image.FillMethod.Radial180;
            case ReloadFillMethod.Radial90: return Image.FillMethod.Radial90;
            case ReloadFillMethod.Horizontal: return Image.FillMethod.Horizontal;
            case ReloadFillMethod.Vertical: return Image.FillMethod.Vertical;
            default: return Image.FillMethod.Radial360;
        }
    }

    /// <summary>
    /// Só resolve referências e procura objetos existentes. Nada aqui cria hierarquia: os
    /// retículos e seus sprites são responsabilidade da cena/prefab.
    /// </summary>
    private void ResolveReferences()
    {
        if (turretAim == null)
            turretAim = GetComponent<TankTurretAim>();

        if (turretAim == null)
            turretAim = FindFirstObjectByType<TankTurretAim>();

        if (barrelTransform == null && turretAim != null)
            barrelTransform = turretAim.AimReference;

        if (barrelTransform == null)
            barrelTransform = FindChildByName(transform, "Trabuco");

        if (barrelOrigin == null)
            barrelOrigin = FindChildByName(transform, "FX_Tiro");

        if (aimCamera == null)
            aimCamera = turretAim != null ? turretAim.AimCamera : Camera.main;

        // O atirador costuma estar no mesmo GameObject (Tanque); se o HUD for movido para o
        // Canvas da HUD, o FindFirstObjectByType mantém a animação de recarga funcionando.
        if (shooter == null)
            shooter = GetComponent<TankPlayerInput>();

        if (shooter == null)
            shooter = GetComponentInParent<TankPlayerInput>();

        if (shooter == null)
            shooter = FindFirstObjectByType<TankPlayerInput>();

        if (hudCanvas == null)
        {
            if (cameraReticle != null)
                hudCanvas = cameraReticle.GetComponentInParent<Canvas>();
            else if (barrelReticle != null)
                hudCanvas = barrelReticle.GetComponentInParent<Canvas>();
        }

        if (canvasRectTransform == null && hudCanvas != null)
            canvasRectTransform = hudCanvas.GetComponent<RectTransform>();

        // Só procura objetos que já existem na cena; o RectTransform é obtido por cast, porque
        // num elemento de UI o próprio Transform do GameObject já é um RectTransform.
        if (cameraReticle == null && hudCanvas != null)
            cameraReticle = (RectTransform)FindChildByName(hudCanvas.transform, "ReticuloCamera");

        if (barrelReticle == null && hudCanvas != null)
            barrelReticle = (RectTransform)FindChildByName(hudCanvas.transform, "ReticuloCanhao");
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != root && children[i].name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                return children[i];
        }

        return null;
    }

    private bool ValidateSetup()
    {
        if (missingReferencesReported)
            return false;

        if (aimCamera == null)
        {
            Debug.LogError($"[{nameof(TankAimReticleHUD)}] Nenhuma câmera de mira válida em '{name}'.", this);
            missingReferencesReported = true;
            enabled = false;
            return false;
        }

        if (hudCanvas == null || canvasRectTransform == null)
        {
            Debug.LogError($"[{nameof(TankAimReticleHUD)}] Canvas dos retículos não encontrado. " +
                           "Arraste o Canvas (ex.: HUD_Mira) em Hud Canvas, ou atribua os " +
                           "RectTransforms dos retículos — o script não cria mais a HUD.", this);
            missingReferencesReported = true;
            enabled = false;
            return false;
        }

        if (cameraReticle == null && barrelReticle == null)
        {
            Debug.LogError($"[{nameof(TankAimReticleHUD)}] Nenhum retículo atribuído em '{name}' " +
                           "(Camera Reticle e Barrel Reticle). Coloque os objetos da cena com seus " +
                           "respectivos Source Image e arraste-os aqui.", this);
            missingReferencesReported = true;
            enabled = false;
            return false;
        }

        if (cameraReticle == null)
            Debug.LogWarning($"[{nameof(TankAimReticleHUD)}] Sem retículo central: só o do canhão será usado.", this);

        if (barrelReticle == null)
            Debug.LogWarning($"[{nameof(TankAimReticleHUD)}] Sem retículo do canhão: sem offset e sem " +
                             "animação de recarga.", this);

        return true;
    }

    #endregion

    private void OnValidate()
    {
        aimDistance = Mathf.Max(1f, aimDistance);
        minReloadDuration = Mathf.Max(0.01f, minReloadDuration);
        fillOrigin = Mathf.Clamp(fillOrigin, 0, 3);
    }
}
