using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Teclado virtual compacto (A–Z, 0–9) para o campo de apelido (3 caracteres) do Game Over
/// quando o jogador está usando controle.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/UI/Teclado Virtual Nickname")]
public sealed class TecladoVirtualNickname : MonoBehaviour
{
    private const string Caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private TMP_InputField alvo;
    private CanvasGroup grupoDaJanela;
    private Selectable focoAoConfirmar;
    private GameObject painelTeclado;
    private Button primeiraTecla;
    private Button botaoOk;

    private float abertoEm;
    private bool bloqueadoPorTecladoFisico;

    public bool EstaAberto => painelTeclado != null && painelTeclado.activeSelf;

    /// <summary>
    /// Evita reabrir imediatamente no mesmo frame caso o foco retorne ao input ao fechar.
    /// </summary>
    public float FechadoEm { get; private set; } = -10f;

    public void Configurar(TMP_InputField input, CanvasGroup grupoJanela, Selectable proximoFoco)
    {
        alvo = input;
        grupoDaJanela = grupoJanela;
        focoAoConfirmar = proximoFoco;

        ConstruirSeNecessario();
        if (painelTeclado != null) painelTeclado.SetActive(false);
    }

    private void Update()
    {
        if (!EstaAberto) return;

        // Se o jogador encostar no teclado físico, fecha o teclado virtual e devolve edição direta.
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            FecharSemMoverFoco();
            if (alvo != null)
            {
                alvo.shouldActivateOnSelect = true;
                alvo.ActivateInputField();
            }
            return;
        }

        Gamepad pad = Gamepad.current;
        if (pad == null || Time.unscaledTime - abertoEm < 0.25f) return;

        if (pad.buttonWest.wasPressedThisFrame)
            ApagarUltimo();
        else if (pad.buttonEast.wasPressedThisFrame)
            Fechar(irParaConfirmar: false);
    }

    public void Abrir()
    {
        ConstruirSeNecessario();
        if (painelTeclado == null || alvo == null) return;
        if (EstaAberto) return;

        abertoEm = Time.unscaledTime;
        bloqueadoPorTecladoFisico = false;

        alvo.shouldActivateOnSelect = false;
        alvo.DeactivateInputField();

        if (grupoDaJanela != null) grupoDaJanela.interactable = false;

        painelTeclado.SetActive(true);

        if (EventSystem.current != null && primeiraTecla != null)
            EventSystem.current.SetSelectedGameObject(primeiraTecla.gameObject);
    }

    public void Fechar(bool irParaConfirmar)
    {
        if (!EstaAberto) return;

        FechadoEm = Time.unscaledTime;
        painelTeclado.SetActive(false);

        if (grupoDaJanela != null) grupoDaJanela.interactable = true;

        Selectable destino = irParaConfirmar ? focoAoConfirmar : alvo;
        if (destino != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(destino.gameObject);
    }

    public void FecharSemMoverFoco()
    {
        if (!EstaAberto) return;

        FechadoEm = Time.unscaledTime;
        painelTeclado.SetActive(false);
        if (grupoDaJanela != null) grupoDaJanela.interactable = true;
    }

    private void Inserir(char caractere)
    {
        if (bloqueadoPorTecladoFisico || Time.unscaledTime - abertoEm < 0.25f || alvo == null)
            return;

        int limite = alvo.characterLimit > 0 ? alvo.characterLimit : 3;
        string atual = alvo.text ?? string.Empty;
        if (atual.Length >= limite) return;

        alvo.text = atual + caractere;

        // Ao preencher o 3º caractere, move o foco direto para "OK".
        if (alvo.text.Length >= limite && botaoOk != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(botaoOk.gameObject);
    }

    private void ApagarUltimo()
    {
        if (Time.unscaledTime - abertoEm < 0.25f || alvo == null) return;
        if (string.IsNullOrEmpty(alvo.text)) return;

        alvo.text = alvo.text.Substring(0, alvo.text.Length - 1);
    }

    private void Limpar()
    {
        if (Time.unscaledTime - abertoEm < 0.25f || alvo == null) return;
        alvo.text = string.Empty;
    }

    private void ConstruirSeNecessario()
    {
        if (painelTeclado != null) return;

        TMP_FontAsset fonte = alvo != null && alvo.textComponent != null ? alvo.textComponent.font : null;

        painelTeclado = new GameObject("TecladoVirtualPanel", typeof(RectTransform), typeof(Image));
        painelTeclado.transform.SetParent(transform, false);

        // Posicionado abaixo do NicknameInput (y = +13 no Card), mantendo o campo visível acima.
        RectTransform rt = painelTeclado.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(680f, 330f);
        rt.anchoredPosition = new Vector2(0f, -205f);

        painelTeclado.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.96f);

        GameObject gradeObj = new GameObject("GradeTeclas", typeof(RectTransform), typeof(GridLayoutGroup));
        gradeObj.transform.SetParent(painelTeclado.transform, false);

        RectTransform rtGrade = gradeObj.GetComponent<RectTransform>();
        rtGrade.anchorMin = new Vector2(0f, 0.22f);
        rtGrade.anchorMax = new Vector2(1f, 1f);
        rtGrade.offsetMin = new Vector2(16f, 8f);
        rtGrade.offsetMax = new Vector2(-16f, -14f);

        GridLayoutGroup grid = gradeObj.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;
        grid.cellSize = new Vector2(64f, 50f);
        grid.spacing = new Vector2(8f, 8f);
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < Caracteres.Length; i++)
        {
            char c = Caracteres[i];
            Button btn = CriarBotao(gradeObj.transform, c.ToString(), fonte, () => Inserir(c));
            if (i == 0) primeiraTecla = btn;
        }

        GameObject acoesObj = new GameObject("Acoes", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        acoesObj.transform.SetParent(painelTeclado.transform, false);

        RectTransform rtAcoes = acoesObj.GetComponent<RectTransform>();
        rtAcoes.anchorMin = new Vector2(0f, 0f);
        rtAcoes.anchorMax = new Vector2(1f, 0.22f);
        rtAcoes.offsetMin = new Vector2(16f, 12f);
        rtAcoes.offsetMax = new Vector2(-16f, -6f);

        HorizontalLayoutGroup hbox = acoesObj.GetComponent<HorizontalLayoutGroup>();
        hbox.spacing = 12f;
        hbox.childControlWidth = true;
        hbox.childControlHeight = true;
        hbox.childForceExpandWidth = true;
        hbox.childForceExpandHeight = true;

        CriarBotao(acoesObj.transform, "Apagar (X)", fonte, ApagarUltimo);
        CriarBotao(acoesObj.transform, "Limpar", fonte, Limpar);
        botaoOk = CriarBotao(acoesObj.transform, "OK (B)", fonte, () => Fechar(irParaConfirmar: true));
    }

    private static Button CriarBotao(Transform pai, string rotulo, TMP_FontAsset fonte, Action aoClicar)
    {
        GameObject go = new GameObject("Tecla_" + rotulo, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(pai, false);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.26f, 0.36f, 1f);

        Button btn = go.GetComponent<Button>();
        ColorBlock cores = btn.colors;
        cores.normalColor = new Color(0.2f, 0.26f, 0.36f, 1f);
        cores.highlightedColor = new Color(0.3f, 0.48f, 0.78f, 1f);
        cores.selectedColor = new Color(0.2f, 0.55f, 0.95f, 1f);
        cores.pressedColor = new Color(0.15f, 0.4f, 0.75f, 1f);
        btn.colors = cores;
        btn.onClick.AddListener(() => aoClicar?.Invoke());

        GameObject txtObj = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(go.transform, false);

        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = Vector2.zero;
        rtTxt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        if (fonte != null) tmp.font = fonte;
        tmp.text = rotulo;
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }
}