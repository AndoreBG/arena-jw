#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ArenaSetup
{
    /// <summary>
    /// Utilitários compartilhados pelos scripts de Editor da Arena: criação de pastas/assets,
    /// localização do InputActionAsset, configuração de propriedades privadas [SerializeField]
    /// e fábrica de controles de UI (uGUI + TextMeshPro).
    /// </summary>
    public static class ArenaEditorUtil
    {
        public const string ScriptableObjectsRoot = "Assets/ScriptableObjects";
        public const string ResourcesRoot = "Assets/Resources";
        public const string MenuScenePath = "Assets/Scenes/menu_principal.unity";
        public const string PlaygroundScenePath = "Assets/Scenes/playground.unity";
        public const string EnemyPrefabPath = "Assets/Prefabs/Inimigo.prefab";
        public const string GameInitializerPath = "Assets/Prefabs/ArenaGame.prefab";

        private static TMP_FontAsset cachedFont;
        private static Sprite spriteKnob, spriteBackground, spriteInputField, spriteCheckmark, spriteDropdown, spriteMask, spriteUISprite;

        #region Pastas e assets

        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }

        public static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        #endregion

        #region Input Actions

        /// <summary>Localiza o asset de Input Actions do projeto (o TankPlayerInput da cena aponta para ele).</summary>
        public static UnityEngine.InputSystem.InputActionAsset FindInputActions()
        {
            string[] guids = AssetDatabase.FindAssets("TankInputActions t:InputActionAsset");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));

            guids = AssetDatabase.FindAssets("t:InputActionAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("TankInputActions.inputactions")) return AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
            }

            return null;
        }

        #endregion

        #region Serialized properties

        /// <summary>Atribui um valor a um campo [SerializeField] privado de um componente.</summary>
        public static void SetField(Object target, string fieldName, object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[Arena] Campo '{fieldName}' não existe em {target.GetType().Name}.");
                return;
            }

            ApplyValue(property, value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Lê um campo [SerializeField] privado (para relatórios e idempotência).</summary>
        public static object GetField(Object target, string fieldName)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null) return null;
            return SerializedValue(property);
        }

        private static void ApplyValue(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as Object;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = value is float f ? f : System.Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = value is int i ? i : System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = System.Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = System.Convert.ToString(value);
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.LayerMask:
                    property.intValue = value is LayerMask mask ? mask.value : System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = value is Vector2 v2 ? v2 : (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = value is Vector3 v3 ? v3 : (Vector3)value;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value;
                    break;
                default:
                    if (property.isArray && value is System.Array array)
                    {
                        property.arraySize = array.Length;
                        for (int index = 0; index < array.Length; index++)
                        {
                            SerializedProperty element = property.GetArrayElementAtIndex(index);
                            element.objectReferenceValue = array.GetValue(index) as Object;
                        }
                        break;
                    }
                    Debug.LogWarning($"[Arena] Tipo de propriedade não suportado: {property.propertyType} ({property.propertyPath}).");
                    break;
            }
        }

        private static object SerializedValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue;
                case SerializedPropertyType.Float: return property.floatValue;
                case SerializedPropertyType.Integer: return property.intValue;
                case SerializedPropertyType.Boolean: return property.boolValue;
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Enum: return property.enumValueIndex;
                case SerializedPropertyType.LayerMask: return property.intValue;
                case SerializedPropertyType.Vector2: return property.vector2Value;
                case SerializedPropertyType.Vector3: return property.vector3Value;
                case SerializedPropertyType.Color: return property.colorValue;
                default: return null;
            }
        }

        #endregion

        #region Fonte e sprites

        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (cachedFont == null) cachedFont = TMP_Settings.defaultFontAsset;
                if (cachedFont == null)
                {
                    string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
                    if (guids.Length > 0)
                        cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
                return cachedFont;
            }
        }

        private static Sprite LoadBuiltinSprite(string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }

        /// <summary>Recursos dos controles padrão (uGUI + TMP) para montar Slider/Dropdown/InputField.</summary>
        public static TMP_DefaultControls.Resources TmpResources()
        {
            var resources = new TMP_DefaultControls.Resources();

            if (spriteBackground == null) spriteBackground = LoadBuiltinSprite("UI/Skin/Background.psd");
            if (spriteInputField == null) spriteInputField = LoadBuiltinSprite("UI/Skin/InputFieldBackground.psd");
            if (spriteKnob == null) spriteKnob = LoadBuiltinSprite("UI/Skin/Knob.psd");
            if (spriteCheckmark == null) spriteCheckmark = LoadBuiltinSprite("UI/Skin/Checkmark.psd");
            if (spriteDropdown == null) spriteDropdown = LoadBuiltinSprite("UI/Skin/DropdownArrow.psd");
            if (spriteMask == null) spriteMask = LoadBuiltinSprite("UI/Skin/UIMask.psd");

            resources.standard = spriteUISprite != null ? spriteUISprite : (spriteUISprite = LoadBuiltinSprite("UI/Skin/UISprite.psd"));
            resources.background = spriteBackground;
            resources.inputField = spriteInputField;
            resources.knob = spriteKnob;
            resources.checkmark = spriteCheckmark;
            resources.dropdown = spriteDropdown;
            resources.mask = spriteMask;

            return resources;
        }

        #endregion

        #region Canvas e EventSystem

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>Garante um EventSystem compatível com o Input System (não duplica).</summary>
        public static EventSystem EnsureEventSystem()
        {
            EventSystem existing = Object.FindFirstObjectByType<EventSystem>(UnityEngine.FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.GetComponent<InputSystemUIInputModule>() == null &&
                    existing.GetComponent<StandaloneInputModule>() != null)
                {
                    // O projeto usa Input System (activeInputHandler = 1); o StandaloneInputModule
                    // antigo falha silenciosamente nesse modo.
                    Object.DestroyImmediate(existing.GetComponent<StandaloneInputModule>());
                    existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }
                return existing;
            }

            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return go.GetComponent<EventSystem>();
        }

        #endregion

        #region Controles

        public static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size,
            TextAlignmentOptions alignment, Color color, bool bold = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (DefaultFont != null) text.font = DefaultFont;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            if (bold) text.fontStyle = FontStyles.Bold;

            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = color;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI text = CreateText(go.transform, "Label", label, 28f, TextAlignmentOptions.Center, Color.white);
            Stretch((RectTransform)text.transform);

            return button;
        }

        /// <summary>
        /// Monta um Slider no padrão do uGUI (Background / Fill Area / Fill / Handle Slide Area /
        /// Handle) sem depender de fábrica: nem TMP_DefaultControls nem
        /// UnityEngine.UI.DefaultControls têm CreateSlider exposto em todas as versões.
        /// </summary>
        public static Slider CreateSlider(Transform parent, string name)
        {
            RectTransform root = CreateRect(parent, name);
            root.sizeDelta = new Vector2(420f, 32f);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            slider.direction = Slider.Direction.LeftToRight;

            Sprite backgroundSprite = LoadBuiltinSprite("UI/Skin/Background.psd");
            Sprite fillSprite = LoadBuiltinSprite("UI/Skin/UISprite.psd");
            Sprite knobSprite = LoadBuiltinSprite("UI/Skin/Knob.psd");

            // Trilho
            Image background = CreateImage(root, "Background", backgroundSprite, new Color(1f, 1f, 1f, 0.30f));
            background.rectTransform.anchorMin = new Vector2(0f, 0.3f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.7f);
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;

            // Área de preenchimento
            RectTransform fillArea = CreateRect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.3f);
            fillArea.anchorMax = new Vector2(1f, 0.7f);
            fillArea.offsetMin = new Vector2(9f, 0f);
            fillArea.offsetMax = new Vector2(-9f, 0f);

            Image fill = CreateImage(fillArea, "Fill", fillSprite, new Color(0.20f, 0.45f, 0.85f, 1f));
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(10f, 0f);
            fillRect.anchoredPosition = Vector2.zero;

            // Área do botão
            RectTransform handleArea = CreateRect(root, "Handle Slide Area");
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(9f, 0f);
            handleArea.offsetMax = new Vector2(-9f, 0f);

            Image handle = CreateImage(handleArea, "Handle", knobSprite, Color.white);
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(20f, 0f);
            handleRect.anchoredPosition = Vector2.zero;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;

            return slider;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            return image;
        }

        public static TMP_Dropdown CreateDropdown(Transform parent, string name)
        {
            GameObject go = Object.Instantiate(TMP_DefaultControls.CreateDropdown(TmpResources()));
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 52f);
            return go.GetComponent<TMP_Dropdown>();
        }

        public static TMP_InputField CreateInputField(Transform parent, string name, int characterLimit)
        {
            GameObject go = Object.Instantiate(TMP_DefaultControls.CreateInputField(TmpResources()));
            go.name = name;
            go.transform.SetParent(parent, false);

            TMP_InputField input = go.GetComponent<TMP_InputField>();
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            input.restoreOriginalTextOnEscape = false;

            return input;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        #endregion
    }
}
#endif
