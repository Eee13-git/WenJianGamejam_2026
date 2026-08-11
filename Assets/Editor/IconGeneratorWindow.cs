using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 图标生成器 — Editor 工具，通过配置边框/填充/文字生成 Sprite 图标。
/// 菜单: Tools > Icon Generator
/// 属性通过 EditorPrefs 持久化，关闭再打开后保持上次配置。
/// 
/// 原理：高分辨率纹理保证清晰度，自动 PPU 保证场景中尺寸一致。
/// 项目其他道具图标为 64×64/PPU100=0.64世界单位，
/// 本生成器按 PPU = texSize / 0.64 输出，无论分辨率多少场景中都是 0.64。
/// </summary>
public class IconGeneratorWindow : EditorWindow
{
    public enum BorderShape { Circle, Square, RoundedSquare }

    // === 配置 ===
    private BorderShape _shape = BorderShape.Circle;
    private int _textureSize = 256;
    private int _borderWidth = 12;
    private Color _borderColor = new Color(0.86f, 0.08f, 0.24f, 1f);
    private Color _fillColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
    private float _cornerRadius = 0.3f;

    private string _text = "C9H13NO3";
    private Color _textColor = new Color(1f, 0.85f, 0.2f, 1f);
    private int _fontSize = 28;
    private TMP_FontAsset _fontAsset;
    private float _textPadding = 0.15f;

    private string _outputPath = "Assets/GeneratedIcon.png";

    // === 数字样式（文字内容中的数字字符独立样式） ===
    private bool _digitStyleEnabled = false;
    private TMP_FontAsset _digitFontAsset;
    private int _digitFontSize = 28;
    private Color _digitColor = new Color(1f, 1f, 1f, 1f);

    private Texture2D _previewTexture;

    /// <summary>道具图标在场景中的标准世界尺寸</summary>
    private float _iconWorldSize = 0.64f;

    // EditorPrefs Keys
    private const string k_Shape = "IconGen_Shape";
    private const string k_TextureSize = "IconGen_TextureSize";
    private const string k_BorderWidth = "IconGen_BorderWidth";
    private const string k_BorderColor = "IconGen_BorderColor";
    private const string k_FillColor = "IconGen_FillColor";
    private const string k_CornerRadius = "IconGen_CornerRadius";
    private const string k_Text = "IconGen_Text";
    private const string k_TextColor = "IconGen_TextColor";
    private const string k_FontSize = "IconGen_FontSize";
    private const string k_FontGUID = "IconGen_FontGUID";
    private const string k_TextPadding = "IconGen_TextPadding";
    private const string k_OutputPath = "IconGen_OutputPath";
    private const string k_IconWorldSize = "IconGen_IconWorldSize";
    private const string k_DigitStyleEnabled = "IconGen_DigitStyleEnabled";
    private const string k_DigitFontSize = "IconGen_DigitFontSize";
    private const string k_DigitColor = "IconGen_DigitColor";
    private const string k_DigitFontGUID = "IconGen_DigitFontGUID";

    [MenuItem("Tools/Icon Generator")]
    public static void Open()
    {
        var window = GetWindow<IconGeneratorWindow>("图标生成器");
        window.minSize = new Vector2(360, 520);
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void OnDisable()
    {
        SaveSettings();
    }

    // ==================== 持久化 ====================

    private void LoadSettings()
    {
        _shape = (BorderShape)EditorPrefs.GetInt(k_Shape, (int)BorderShape.Circle);
        _textureSize = EditorPrefs.GetInt(k_TextureSize, 256);
        _borderWidth = EditorPrefs.GetInt(k_BorderWidth, 12);
        _borderColor = LoadColor(k_BorderColor, new Color(0.86f, 0.08f, 0.24f, 1f));
        _fillColor = LoadColor(k_FillColor, new Color(0.12f, 0.12f, 0.15f, 0.9f));
        _cornerRadius = EditorPrefs.GetFloat(k_CornerRadius, 0.3f);
        _text = EditorPrefs.GetString(k_Text, "C9H13NO3");
        _textColor = LoadColor(k_TextColor, new Color(1f, 0.85f, 0.2f, 1f));
        _fontSize = EditorPrefs.GetInt(k_FontSize, 28);
        _textPadding = EditorPrefs.GetFloat(k_TextPadding, 0.15f);
        _outputPath = EditorPrefs.GetString(k_OutputPath, "Assets/GeneratedIcon.png");
        _iconWorldSize = EditorPrefs.GetFloat(k_IconWorldSize, 0.64f);
        _digitStyleEnabled = EditorPrefs.GetBool(k_DigitStyleEnabled, false);
        _digitFontSize = EditorPrefs.GetInt(k_DigitFontSize, 28);
        _digitColor = LoadColor(k_DigitColor, new Color(1f, 1f, 1f, 1f));

        string fontGUID = EditorPrefs.GetString(k_FontGUID, "");
        if (!string.IsNullOrEmpty(fontGUID))
        {
            var path = AssetDatabase.GUIDToAssetPath(fontGUID);
            if (!string.IsNullOrEmpty(path))
                _fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        string digitFontGUID = EditorPrefs.GetString(k_DigitFontGUID, "");
        if (!string.IsNullOrEmpty(digitFontGUID))
        {
            var digitPath = AssetDatabase.GUIDToAssetPath(digitFontGUID);
            if (!string.IsNullOrEmpty(digitPath))
                _digitFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(digitPath);
        }
    }

    private void SaveSettings()
    {
        EditorPrefs.SetInt(k_Shape, (int)_shape);
        EditorPrefs.SetInt(k_TextureSize, _textureSize);
        EditorPrefs.SetInt(k_BorderWidth, _borderWidth);
        SaveColor(k_BorderColor, _borderColor);
        SaveColor(k_FillColor, _fillColor);
        EditorPrefs.SetFloat(k_CornerRadius, _cornerRadius);
        EditorPrefs.SetString(k_Text, _text);
        SaveColor(k_TextColor, _textColor);
        EditorPrefs.SetInt(k_FontSize, _fontSize);
        EditorPrefs.SetFloat(k_TextPadding, _textPadding);
        EditorPrefs.SetString(k_OutputPath, _outputPath);

        if (_fontAsset != null)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_fontAsset));
            EditorPrefs.SetString(k_FontGUID, guid);
        }
        else
        {
            EditorPrefs.SetString(k_FontGUID, "");
        }

        EditorPrefs.SetFloat(k_IconWorldSize, _iconWorldSize);
        EditorPrefs.SetBool(k_DigitStyleEnabled, _digitStyleEnabled);
        EditorPrefs.SetInt(k_DigitFontSize, _digitFontSize);
        SaveColor(k_DigitColor, _digitColor);

        if (_digitFontAsset != null)
        {
            var digitGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_digitFontAsset));
            EditorPrefs.SetString(k_DigitFontGUID, digitGuid);
        }
        else
        {
            EditorPrefs.SetString(k_DigitFontGUID, "");
        }
    }

    private static Color LoadColor(string key, Color defaultVal)
    {
        string s = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(s)) return defaultVal;
        var parts = s.Split(',');
        if (parts.Length != 4) return defaultVal;
        return new Color(
            float.Parse(parts[0]),
            float.Parse(parts[1]),
            float.Parse(parts[2]),
            float.Parse(parts[3])
        );
    }

    private static void SaveColor(string key, Color val)
    {
        EditorPrefs.SetString(key, $"{val.r},{val.g},{val.b},{val.a}");
    }

    // ==================== UI ====================

    private void OnGUI()
    {
        GUILayout.Label("图标生成器", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _shape = (BorderShape)EditorGUILayout.EnumPopup("边框形状", _shape);
        _textureSize = EditorGUILayout.IntSlider("纹理尺寸", _textureSize, 64, 1024);
        _borderWidth = EditorGUILayout.IntSlider("边框宽度", _borderWidth, 1, 40);
        _borderColor = EditorGUILayout.ColorField("边框颜色", _borderColor);
        _fillColor = EditorGUILayout.ColorField("内部填充", _fillColor);

        if (_shape == BorderShape.RoundedSquare)
            _cornerRadius = EditorGUILayout.Slider("圆角比例", _cornerRadius, 0f, 0.5f);

        EditorGUILayout.Space(6);

        GUILayout.Label("文字", EditorStyles.boldLabel);
        _text = EditorGUILayout.TextField("内容", _text);
        _textColor = EditorGUILayout.ColorField("文字颜色", _textColor);
        _fontSize = EditorGUILayout.IntSlider("字号", _fontSize, 2, 80);
        _fontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("字体 (TMP)", _fontAsset, typeof(TMP_FontAsset), false);
        _textPadding = EditorGUILayout.Slider("文字内边距", _textPadding, 0f, 0.4f);

        EditorGUILayout.Space(6);

        // === 数字样式（文字内容中的数字字符独立样式） ===
        _digitStyleEnabled = EditorGUILayout.BeginToggleGroup("数字独立样式", _digitStyleEnabled);
        _digitColor = EditorGUILayout.ColorField("数字颜色", _digitColor);
        _digitFontSize = EditorGUILayout.IntSlider("数字字号", _digitFontSize, 2, 80);
        _digitFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("数字字体 (TMP)", _digitFontAsset, typeof(TMP_FontAsset), false);
        EditorGUILayout.EndToggleGroup();

        EditorGUILayout.Space(6);

        GUILayout.Label("输出", EditorStyles.boldLabel);
        _outputPath = EditorGUILayout.TextField("保存路径", _outputPath);
        _iconWorldSize = EditorGUILayout.Slider("图标场景尺寸", _iconWorldSize, 0.1f, 2f);

        // 显示自动计算的 PPU
        float autoPPU = _textureSize / _iconWorldSize;
        EditorGUILayout.LabelField("自动 PPU", $"{autoPPU:F0} (场景中 {_iconWorldSize:F2} 单位)");

        EditorGUILayout.Space(8);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("预览", GUILayout.Height(30)))
            _previewTexture = GenerateTexture();
        if (GUILayout.Button("生成并保存", GUILayout.Height(30)))
        {
            _previewTexture = GenerateTexture();
            SaveTexture(_previewTexture);
        }
        GUILayout.EndHorizontal();

        if (_previewTexture != null)
        {
            EditorGUILayout.Space(8);
            GUILayout.Label("预览:", EditorStyles.boldLabel);
            int previewSize = Mathf.Max(128, _textureSize);
            var rect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(true));
            if (rect.width > previewSize) rect.x += (rect.width - previewSize) / 2f;
            rect.width = previewSize;
            rect.height = previewSize;
            GUI.DrawTexture(rect, _previewTexture, ScaleMode.ScaleToFit);
        }
    }

    // ==================== 生成逻辑 ====================

    public Texture2D GenerateTexture()
    {
        int size = _textureSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f - 1f;
        float innerRadius = outerRadius - _borderWidth;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Color c;

                switch (_shape)
                {
                    case BorderShape.Circle:
                        c = SampleCircle(p, center, outerRadius, innerRadius);
                        break;
                    case BorderShape.Square:
                        c = SampleSquare(p, size, _borderWidth);
                        break;
                    case BorderShape.RoundedSquare:
                        c = SampleRoundedSquare(p, size, _borderWidth, _cornerRadius);
                        break;
                    default:
                        c = Color.clear;
                        break;
                }
                tex.SetPixel(x, y, c);
            }
        }

        if (!string.IsNullOrEmpty(_text))
        {
            var textTex = RenderTextToTexture(size);
            if (textTex != null)
                CompositeTextures(tex, textTex);
        }

        tex.Apply();
        return tex;
    }

    private Color SampleCircle(Vector2 p, Vector2 center, float outerR, float innerR)
    {
        float dist = Vector2.Distance(p, center);
        if (dist <= outerR && dist >= innerR) return _borderColor;
        if (dist < innerR) return _fillColor;
        return Color.clear;
    }

    private Color SampleSquare(Vector2 p, int size, int border)
    {
        bool inX = p.x >= 0 && p.x < size;
        bool inY = p.y >= 0 && p.y < size;
        if (!inX || !inY) return Color.clear;
        bool onBorder = p.x < border || p.x >= size - border || p.y < border || p.y >= size - border;
        return onBorder ? _borderColor : _fillColor;
    }

    private Color SampleRoundedSquare(Vector2 p, int size, int border, float cornerRatio)
    {
        float cornerR = size * cornerRatio;
        bool inside = IsInsideRoundedSquare(p, size, cornerR);
        if (!inside) return Color.clear;
        bool onBorder = !IsInsideRoundedSquare(p, size - border * 2, cornerR - border);
        return onBorder ? _borderColor : _fillColor;
    }

    private bool IsInsideRoundedSquare(Vector2 p, int size, float cornerR)
    {
        float half = size / 2f;
        Vector2 center = new Vector2(half, half);
        Vector2 d = p - center;
        float ax = Mathf.Abs(d.x);
        float ay = Mathf.Abs(d.y);

        float innerHalf = half - cornerR;
        if (innerHalf <= 0) return ax <= half && ay <= half;
        if (ax <= innerHalf && ay <= innerHalf) return true;
        if (ax > half || ay > half) return false;
        float dx = ax - innerHalf;
        float dy = ay - innerHalf;
        if (dx > 0 && dy > 0)
            return Mathf.Sqrt(dx * dx + dy * dy) <= cornerR;
        return true;
    }

    // ==================== 文字渲染 ====================

    /// <summary>
    /// 将文本中的数字字符用富文本标签包裹，使其使用独立的字体/字号/颜色。
    /// 例: "C9H13NO3" → "C<font=..><size=..><color=..>9</..>H<font=..>13</..>NO<font=..>3</..>"
    /// </summary>
    private string BuildRichTextWithDigitStyle(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length * 3);

        // 颜色: #RRGGBBAA
        string hexColor = ColorUtility.ToHtmlStringRGBA(_digitColor);
        int digitSize = _digitFontSize;
        string fontName = _digitFontAsset != null ? _digitFontAsset.name : null;

        bool inDigit = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool isDigit = char.IsDigit(c);

            if (isDigit && !inDigit)
            {
                // 开启数字样式标签
                if (fontName != null)
                    sb.Append($"<font=\"{fontName}\">");
                sb.Append($"<size={digitSize}>");
                sb.Append($"<color=#{hexColor}>");
                inDigit = true;
            }
            else if (!isDigit && inDigit)
            {
                // 关闭数字样式标签
                sb.Append("</color></size>");
                if (fontName != null)
                    sb.Append("</font>");
                inDigit = false;
            }

            sb.Append(c);
        }

        // 收尾关闭标签
        if (inDigit)
        {
            sb.Append("</color></size>");
            if (fontName != null)
                sb.Append("</font>");
        }

        return sb.ToString();
    }

    private Texture2D RenderTextToTexture(int size)
    {
        var root = new GameObject("[IconTextRenderer]");
        root.layer = 5;

        var camGo = new GameObject("Cam");
        camGo.transform.SetParent(root.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = size / 2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = 1 << 5;
        cam.transform.position = new Vector3(0, 0, -10);

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(root.transform, false);
        canvasGo.layer = 5;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>();
        var canvasRt = canvas.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(size, size);
        canvasRt.position = Vector3.zero;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(canvasGo.transform, false);
        textGo.layer = 5;
        var textRt = textGo.AddComponent<RectTransform>();
        float pad = size * _textPadding;
        textRt.sizeDelta = new Vector2(size - pad * 2, size - pad * 2);
        textRt.position = new Vector3(0, 0, -1);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = _fontSize;
        tmp.color = _textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = true;
        if (_fontAsset != null) tmp.font = _fontAsset;

        // === 数字独立样式：构建富文本 ===
        if (_digitStyleEnabled && !string.IsNullOrEmpty(_text))
        {
            tmp.richText = true;

            // 数字字体加入主字体的 fallback，使 <font> 标签可查找
            bool addedFallback = false;
            if (_digitFontAsset != null && tmp.font != null)
            {
                if (tmp.font.fallbackFontAssetTable == null)
                    tmp.font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (!tmp.font.fallbackFontAssetTable.Contains(_digitFontAsset))
                {
                    tmp.font.fallbackFontAssetTable.Add(_digitFontAsset);
                    addedFallback = true;
                }
            }

            tmp.text = BuildRichTextWithDigitStyle(_text);

            tmp.ForceMeshUpdate();
            float textWidth = tmp.preferredWidth;
            float textHeight = tmp.preferredHeight;
            float maxW = size - pad * 2;
            float maxH = size - pad * 2;
            if (textWidth > maxW || textHeight > maxH)
            {
                float scale = Mathf.Min(maxW / textWidth, maxH / textHeight);
                tmp.rectTransform.localScale = Vector3.one * scale;
            }

            // 移除临时 fallback
            if (addedFallback)
                tmp.font.fallbackFontAssetTable.Remove(_digitFontAsset);
        }
        else
        {
            tmp.richText = false;
            tmp.text = _text;

            tmp.ForceMeshUpdate();
            float textWidth = tmp.preferredWidth;
            float textHeight = tmp.preferredHeight;
            float maxW = size - pad * 2;
            float maxH = size - pad * 2;
            if (textWidth > maxW || textHeight > maxH)
            {
                float scale = Mathf.Min(maxW / textWidth, maxH / textHeight);
                tmp.rectTransform.localScale = Vector3.one * scale;
            }
        }

        Canvas.ForceUpdateCanvases();
        cam.Render();

        RenderTexture.active = rt;
        var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        result.Apply();
        RenderTexture.active = null;

        Object.DestroyImmediate(root);
        rt.Release();
        Object.DestroyImmediate(rt);

        return result;
    }

    private void CompositeTextures(Texture2D baseTex, Texture2D overlayTex)
    {
        int size = baseTex.width;
        var basePixels = baseTex.GetPixels();
        var overlayPixels = overlayTex.GetPixels();

        for (int i = 0; i < basePixels.Length; i++)
        {
            var bg = basePixels[i];
            var fg = overlayPixels[i];
            float a = fg.a;
            basePixels[i] = new Color(
                fg.r * a + bg.r * (1 - a),
                fg.g * a + bg.g * (1 - a),
                fg.b * a + bg.b * (1 - a),
                Mathf.Max(bg.a, a)
            );
        }
        baseTex.SetPixels(basePixels);
    }

    // ==================== 保存 ====================

    public void SaveTexture(Texture2D tex)
    {
        if (tex == null) return;

        byte[] png = tex.EncodeToPNG();
        string dir = System.IO.Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        System.IO.File.WriteAllBytes(_outputPath, png);
        AssetDatabase.ImportAsset(_outputPath, ImportAssetOptions.ForceUpdate);

        // 自动计算 PPU：使场景中尺寸始终为 TARGET_WORLD_SIZE (0.64)
        float ppu = tex.width / _iconWorldSize;

        var importer = AssetImporter.GetAtPath(_outputPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = Mathf.RoundToInt(ppu);
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[IconGenerator] 图标已保存到 {_outputPath} (PPU={Mathf.RoundToInt(ppu)}, 场景尺寸={_iconWorldSize:F2})");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(_outputPath));
    }
}
