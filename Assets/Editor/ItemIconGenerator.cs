using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

/// <summary>
/// 道具图标生成器 — 在背景精灵上叠加文字，生成新的道具图标纹理。
/// 菜单: Tools/道具图标生成器
/// 支持：背景缩放模式（独立/等比/原始）、文字偏移、自动换行、实时预览。
/// </summary>
public class ItemIconGenerator : EditorWindow
{
    private enum OutputResolution
    {
        Size128 = 128,
        Size256 = 256,
        Size512 = 512
    }

    private enum BackgroundScaleMode
    {
        Custom,   // 独立 X/Y 缩放
        Uniform,  // 等比缩放（单个缩放值）
        Original  // 原始像素尺寸（真实世界大小）
    }

    [Header("输入")]
    [SerializeField] private Sprite _backgroundSprite;
    [SerializeField] private string _text = "道具名";
    [SerializeField] private int _fontSize = 24;
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private OutputResolution _resolution = OutputResolution.Size128;
    [SerializeField] private bool _autoFitFontSize = true;
    [SerializeField] private int _maxCharsPerLine = 0;

    [Header("背景缩放")]
    [SerializeField] private BackgroundScaleMode _bgScaleMode = BackgroundScaleMode.Custom;
    [SerializeField] private float _iconScaleX = 1.0f;
    [SerializeField] private float _iconScaleY = 1.0f;
    [SerializeField] private float _uniformScale = 1.0f;

    [Header("文字偏移（像素）")]
    [SerializeField] private float _textOffsetX = 0f;
    [SerializeField] private float _textOffsetY = 0f;

    [Header("输出")]
    [SerializeField] private string _outputFolder = "Assets/Textures/ItemsIcons/Generated";
    [SerializeField] private string _outputFileName = "icon_new";
    [SerializeField] private float _pixelsPerUnit = 128f;

    // TMP 字体缓存
    private TMP_FontAsset _tmpFont;

    // 预览缓存
    private Texture2D _previewTexture;
    private string _lastPreviewKey;

    private const string PrefsPrefix = "ItemIconGenerator_";

    [MenuItem("Tools/道具图标生成器")]
    public static void ShowWindow()
    {
        GetWindow<ItemIconGenerator>("道具图标生成器");
    }

    private void OnEnable()
    {
        // 加载持久化字段
        _text = EditorPrefs.GetString(PrefsPrefix + "text", "道具名");
        _fontSize = EditorPrefs.GetInt(PrefsPrefix + "fontSize", 24);
        _textColor = LoadColor("textColor", Color.white);
        _resolution = (OutputResolution)EditorPrefs.GetInt(PrefsPrefix + "resolution", (int)OutputResolution.Size128);
        _autoFitFontSize = EditorPrefs.GetBool(PrefsPrefix + "autoFit", true);
        _maxCharsPerLine = EditorPrefs.GetInt(PrefsPrefix + "maxCharsPerLine", 0);

        // 缩放模式相关
        _bgScaleMode = (BackgroundScaleMode)EditorPrefs.GetInt(PrefsPrefix + "bgScaleMode", (int)BackgroundScaleMode.Custom);
        _iconScaleX = EditorPrefs.GetFloat(PrefsPrefix + "iconScaleX", 1.0f);
        _iconScaleY = EditorPrefs.GetFloat(PrefsPrefix + "iconScaleY", 1.0f);
        _uniformScale = EditorPrefs.GetFloat(PrefsPrefix + "uniformScale", 1.0f);

        // 文字偏移
        _textOffsetX = EditorPrefs.GetFloat(PrefsPrefix + "textOffsetX", 0f);
        _textOffsetY = EditorPrefs.GetFloat(PrefsPrefix + "textOffsetY", 0f);

        _outputFolder = EditorPrefs.GetString(PrefsPrefix + "outputFolder", "Assets/Textures/ItemsIcons/Generated");
        _outputFileName = EditorPrefs.GetString(PrefsPrefix + "outputFileName", "icon_new");
        _pixelsPerUnit = EditorPrefs.GetFloat(PrefsPrefix + "pixelsPerUnit", 128f);

        // 对象引用通过 GUID 恢复
        _backgroundSprite = LoadAssetByGuid<Sprite>("bgSprite");
        _tmpFont = LoadAssetByGuid<TMP_FontAsset>("tmpFont");
        if (_tmpFont == null)
        {
            _tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset");
            if (_tmpFont == null && TMP_Settings.defaultFontAsset != null)
                _tmpFont = TMP_Settings.defaultFontAsset;
        }
    }

    private void OnDisable()
    {
        SavePrefs();
        if (_previewTexture != null)
        {
            DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(PrefsPrefix + "text", _text);
        EditorPrefs.SetInt(PrefsPrefix + "fontSize", _fontSize);
        SaveColor("textColor", _textColor);
        EditorPrefs.SetInt(PrefsPrefix + "resolution", (int)_resolution);
        EditorPrefs.SetBool(PrefsPrefix + "autoFit", _autoFitFontSize);
        EditorPrefs.SetInt(PrefsPrefix + "maxCharsPerLine", _maxCharsPerLine);

        EditorPrefs.SetInt(PrefsPrefix + "bgScaleMode", (int)_bgScaleMode);
        EditorPrefs.SetFloat(PrefsPrefix + "iconScaleX", _iconScaleX);
        EditorPrefs.SetFloat(PrefsPrefix + "iconScaleY", _iconScaleY);
        EditorPrefs.SetFloat(PrefsPrefix + "uniformScale", _uniformScale);

        EditorPrefs.SetFloat(PrefsPrefix + "textOffsetX", _textOffsetX);
        EditorPrefs.SetFloat(PrefsPrefix + "textOffsetY", _textOffsetY);

        EditorPrefs.SetString(PrefsPrefix + "outputFolder", _outputFolder);
        EditorPrefs.SetString(PrefsPrefix + "outputFileName", _outputFileName);
        EditorPrefs.SetFloat(PrefsPrefix + "pixelsPerUnit", _pixelsPerUnit);

        SaveAssetGuid("bgSprite", _backgroundSprite);
        SaveAssetGuid("tmpFont", _tmpFont);
    }

    private void SaveColor(string key, Color c)
    {
        EditorPrefs.SetString(PrefsPrefix + key, $"{c.r}|{c.g}|{c.b}|{c.a}");
    }

    private Color LoadColor(string key, Color defaultVal)
    {
        string s = EditorPrefs.GetString(PrefsPrefix + key, null);
        if (string.IsNullOrEmpty(s)) return defaultVal;
        string[] p = s.Split('|');
        if (p.Length != 4) return defaultVal;
        return new Color(
            float.Parse(p[0]), float.Parse(p[1]),
            float.Parse(p[2]), float.Parse(p[3]));
    }

    private void SaveAssetGuid(string key, Object obj)
    {
        if (obj == null) { EditorPrefs.SetString(PrefsPrefix + key, ""); return; }
        string path = AssetDatabase.GetAssetPath(obj);
        string guid = AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString(PrefsPrefix + key, guid);
    }

    private T LoadAssetByGuid<T>(string key) where T : Object
    {
        string guid = EditorPrefs.GetString(PrefsPrefix + key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private void OnGUI()
    {
        GUILayout.Label("道具图标生成器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);
        _backgroundSprite = (Sprite)EditorGUILayout.ObjectField(
            "背景精灵", _backgroundSprite, typeof(Sprite), false);

        EditorGUILayout.LabelField("文字内容（支持换行）");
        _text = EditorGUILayout.TextArea(_text, GUILayout.MinHeight(50));

        _fontSize = EditorGUILayout.IntField("字体大小", _fontSize);
        _textColor = EditorGUILayout.ColorField("文字颜色", _textColor);
        _tmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "TMP 字体", _tmpFont, typeof(TMP_FontAsset), false);
        _resolution = (OutputResolution)EditorGUILayout.EnumPopup(
            "输出分辨率", _resolution);
        _autoFitFontSize = EditorGUILayout.Toggle("自动适配字号", _autoFitFontSize);
        _maxCharsPerLine = EditorGUILayout.IntField(
            new GUIContent("每行最大字符数", "0=不自动换行，>0=超过此字符数自动插入换行"),
            _maxCharsPerLine);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("背景缩放", EditorStyles.boldLabel);
        _bgScaleMode = (BackgroundScaleMode)EditorGUILayout.EnumPopup("缩放模式", _bgScaleMode);

        switch (_bgScaleMode)
        {
            case BackgroundScaleMode.Custom:
                _iconScaleX = EditorGUILayout.Slider("缩放 X", _iconScaleX, 0.1f, 3.0f);
                _iconScaleY = EditorGUILayout.Slider("缩放 Y", _iconScaleY, 0.1f, 3.0f);
                break;
            case BackgroundScaleMode.Uniform:
                _uniformScale = EditorGUILayout.Slider("等比缩放", _uniformScale, 0.1f, 3.0f);
                break;
            case BackgroundScaleMode.Original:
                EditorGUILayout.HelpBox("将使用背景精灵的原始像素尺寸，忽略缩放值。", MessageType.Info);
                break;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("文字偏移（像素）", EditorStyles.boldLabel);
        _textOffsetX = EditorGUILayout.Slider("水平偏移 X", _textOffsetX, -100f, 100f);
        _textOffsetY = EditorGUILayout.Slider("垂直偏移 Y", _textOffsetY, -100f, 100f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
        _outputFolder = EditorGUILayout.TextField("输出文件夹", _outputFolder);
        _outputFileName = EditorGUILayout.TextField("输出文件名", _outputFileName);
        _pixelsPerUnit = EditorGUILayout.FloatField("每单位像素数 (Pixels Per Unit)", _pixelsPerUnit);

        EditorGUILayout.Space();
        if (_tmpFont == null)
            EditorGUILayout.HelpBox(
                "未找到 TMP 字体！请确保 NotoSansSC-Regular SDF.asset 存在。",
                MessageType.Error);

        if (_backgroundSprite == null)
            EditorGUILayout.HelpBox("请选择背景精灵。", MessageType.Warning);

        // ===== 预览区域 =====
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);

        if (GUILayout.Button("刷新预览", GUILayout.Height(20)))
        {
            RefreshPreview();
        }

        if (_previewTexture != null)
        {
            GUILayout.Label(_previewTexture, GUILayout.Width(256), GUILayout.Height(256));
        }
        else
        {
            EditorGUILayout.HelpBox("点击\"刷新预览\"查看效果。", MessageType.Info);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("生成图标", GUILayout.Height(30)))
        {
            GenerateIcon();
        }

        // 自动刷新预览
        if (GUI.changed)
        {
            RefreshPreview();
        }
    }

    /// <summary>
    /// 应用自动换行：当 _maxCharsPerLine > 0 且文本不含手动换行时，按字符数自动断行。
    /// </summary>
    private string ApplyAutoWrap(string text, int maxCharsPerLine)
    {
        if (maxCharsPerLine <= 0) return text;

        if (text.Contains("\n")) return text;

        if (text.Length <= maxCharsPerLine) return text;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && i % maxCharsPerLine == 0)
                sb.Append('\n');
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 计算实际使用的字号：自动适配时根据文字最长行估算。
    /// </summary>
    private int CalcActualFontSize(string text, int baseFontSize, int size)
    {
        if (!_autoFitFontSize) return baseFontSize;

        string[] lines = text.Split('\n');
        int maxLineLen = 0;
        foreach (var line in lines)
            maxLineLen = Mathf.Max(maxLineLen, line.Length);

        if (maxLineLen == 0) return baseFontSize;

        float estimatedWidth = maxLineLen * baseFontSize * 0.8f;
        float maxWidth = size * 0.8f;
        if (estimatedWidth > maxWidth)
        {
            return Mathf.Max(8, Mathf.FloorToInt(maxWidth / (maxLineLen * 0.8f)));
        }
        return baseFontSize;
    }

    private void RefreshPreview()
    {
        if (_backgroundSprite == null || _tmpFont == null || string.IsNullOrEmpty(_text))
            return;

        int size = (int)_resolution;
        string processedText = ApplyAutoWrap(_text, _maxCharsPerLine);
        int actualFontSize = CalcActualFontSize(processedText, _fontSize, size);

        if (_previewTexture != null)
            DestroyImmediate(_previewTexture);

        _previewTexture = RenderIconToTexture(_backgroundSprite, processedText, actualFontSize, _textColor, size);
        Repaint();
    }

    private void GenerateIcon()
    {
        if (_backgroundSprite == null)
        {
            ShowNotification(new GUIContent("请先选择背景精灵！"));
            return;
        }
        if (string.IsNullOrEmpty(_text))
        {
            ShowNotification(new GUIContent("请输入文字内容！"));
            return;
        }
        if (_tmpFont == null)
        {
            ShowNotification(new GUIContent("未找到 TMP 字体！"));
            return;
        }

        int size = (int)_resolution;
        string processedText = ApplyAutoWrap(_text, _maxCharsPerLine);
        int actualFontSize = CalcActualFontSize(processedText, _fontSize, size);

        // 确保输出文件夹存在
        if (!AssetDatabase.IsValidFolder(_outputFolder))
        {
            string parent = Path.GetDirectoryName(_outputFolder);
            string folderName = Path.GetFileName(_outputFolder);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                ShowNotification(new GUIContent($"父文件夹不存在: {parent}"));
                return;
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }

        Texture2D result = RenderIconToTexture(_backgroundSprite, processedText, actualFontSize, _textColor, size);
        if (result == null)
        {
            ShowNotification(new GUIContent("渲染失败！请查看控制台。"));
            return;
        }

        string outputPath = $"{_outputFolder}/{_outputFileName}.png";
        File.WriteAllBytes(outputPath, result.EncodeToPNG());

        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = _pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        var savedAsset = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        EditorGUIUtility.PingObject(savedAsset);

        ShowNotification(new GUIContent($"图标已生成: {outputPath}"));
        Debug.Log($"[ItemIconGenerator] 图标已生成: {outputPath} (字号={actualFontSize}, 文本=\"{processedText.Replace("\n", "\\n")}\")");
    }

    /// <summary>
    /// 渲染图标纹理。背景使用指定的缩放模式，文字使用 TMP 渲染，并应用像素偏移。
    /// </summary>
    private Texture2D RenderIconToTexture(Sprite bgSprite, string text, int fontSize, Color textColor, int size)
    {
        int tempLayer = 30;
        GameObject camGO = null;
        GameObject canvasGO = null;
        RenderTexture rt = null;
        RenderTexture prevRT = RenderTexture.active;

        try
        {
            // 相机
            camGO = new GameObject("IconGenCamera");
            camGO.layer = tempLayer;
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.cullingMask = 1 << tempLayer;
            cam.transparencySortMode = TransparencySortMode.Orthographic;

            rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            cam.targetTexture = rt;

            // Canvas
            canvasGO = new GameObject("IconGenCanvas");
            canvasGO.layer = tempLayer;
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(size, size);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ----- 背景层 -----
            var bgGO = new GameObject("BG");
            bgGO.layer = tempLayer;
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.anchoredPosition = Vector2.zero;

            Vector2 spriteSize = new Vector2(bgSprite.rect.width, bgSprite.rect.height);
            Vector2 targetSize = spriteSize;

            switch (_bgScaleMode)
            {
                case BackgroundScaleMode.Custom:
                    targetSize = new Vector2(spriteSize.x * _iconScaleX, spriteSize.y * _iconScaleY);
                    break;
                case BackgroundScaleMode.Uniform:
                    targetSize = spriteSize * _uniformScale;
                    break;
                case BackgroundScaleMode.Original:
                    targetSize = spriteSize;
                    break;
            }
            bgRT.sizeDelta = targetSize;

            var bgImage = bgGO.AddComponent<Image>();
            bgImage.sprite = bgSprite;
            bgImage.type = Image.Type.Simple;
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;
            bgImage.preserveAspect = (_bgScaleMode != BackgroundScaleMode.Custom);

            // ----- 文字层 -----
            var textGO = new GameObject("Text");
            textGO.layer = tempLayer;
            textGO.transform.SetParent(canvasGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            // 使用全屏拉伸锚点，方便通过 offsetMin/offsetMax 整体偏移
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.pivot = new Vector2(0.5f, 0.5f);
            // 应用像素偏移：同时修改 offsetMin 和 offsetMax，使整个矩形平移
            textRT.offsetMin = new Vector2(_textOffsetX, _textOffsetY);
            textRT.offsetMax = new Vector2(_textOffsetX, _textOffsetY);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = _tmpFont;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            tmp.enableWordWrapping = true;

            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(canvasGO.GetComponent<RectTransform>());

            cam.Render();

            RenderTexture.active = rt;
            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();

            return result;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemIconGenerator] 渲染失败: {e}");
            return null;
        }
        finally
        {
            RenderTexture.active = prevRT;

            if (canvasGO != null) DestroyImmediate(canvasGO);
            if (camGO != null) DestroyImmediate(camGO);
            if (rt != null)
            {
                rt.Release();
                DestroyImmediate(rt);
            }
        }
    }
}