using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 障碍物制造器 — 选择 Sprite，配置参数，一键生成障碍物预制体。
/// 菜单: Tools > 障碍物制造器
/// 单个生成，支持预案保存，配置持久化到 EditorPrefs。
/// </summary>
public class ObstacleMakerWindow : EditorWindow
{
    const string PresetsPrefsKey = "ObstacleMakerWindow_Presets";

    // === 输入 Sprite (单个) ===
    Sprite sourceSprite;

    // === 预制体设置 ===
    Vector2 targetWorldSize = new(1f, 1f);

    // === 材质 ===
    Material spriteMaterial;

    // === 输出 ===
    string outputFolder = "Assets/Prefabs/Obstacles";
    string prefabName = "Obstacle";

    // === 预案系统 ===
    [System.Serializable]
    class ConfigData
    {
        public string sourceSpriteGUID;
        public Vector2 targetWorldSize = new(1f, 1f);
        public string materialGUID;
        public string outputFolder = "Assets/Prefabs/Obstacles";
        public string prefabName = "Obstacle";
    }

    [System.Serializable]
    class PresetData
    {
        public string name;
        public ConfigData config;
    }

    [System.Serializable]
    class PresetList
    {
        public List<PresetData> presets = new();
        public string currentName = "";
    }

    PresetList _presets;
    string _newPresetName = "";
    bool _renaming;
    string _renameBuffer = "";

    Vector2 _scroll;

    [MenuItem("Tools/障碍物制造器")]
    static void ShowWindow() => GetWindow<ObstacleMakerWindow>("障碍物制造器");

    void OnEnable()
    {
        LoadPresets();
    }

    void OnDestroy()
    {
        SaveCurrentToPresets();
        SavePresetsToDisk();
    }

    // ──────────────────────────────────────────────
    //  GUID 工具
    // ──────────────────────────────────────────────

    static string ToGUID(Object obj)
    {
        if (obj == null) return "";
        var path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return "";
        return AssetDatabase.AssetPathToGUID(path);
    }

    static T FromGUID<T>(string guid) where T : Object
    {
        if (string.IsNullOrEmpty(guid)) return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    // ──────────────────────────────────────────────
    //  预案存取
    // ──────────────────────────────────────────────

    void LoadPresets()
    {
        var json = EditorPrefs.GetString(PresetsPrefsKey, "");
        if (!string.IsNullOrEmpty(json))
            _presets = JsonUtility.FromJson<PresetList>(json);

        if (_presets == null || _presets.presets.Count == 0)
        {
            _presets = new PresetList();
            _presets.presets.Add(new PresetData { name = "默认", config = new ConfigData() });
            _presets.currentName = "默认";
        }

        ApplyConfigToFields(GetCurrentPreset()?.config);
    }

    PresetData GetCurrentPreset()
    {
        if (_presets == null || _presets.presets.Count == 0) return null;
        return _presets.presets.FirstOrDefault(p => p.name == _presets.currentName)
               ?? _presets.presets[0];
    }

    void SaveCurrentToPresets()
    {
        var p = GetCurrentPreset();
        if (p != null) p.config = CollectFieldsToConfig();
    }

    void SavePresetsToDisk()
    {
        EditorPrefs.SetString(PresetsPrefsKey, JsonUtility.ToJson(_presets));
    }

    void ApplyConfigToFields(ConfigData d)
    {
        if (d == null)
        {
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
            return;
        }
        sourceSprite = FromGUID<Sprite>(d.sourceSpriteGUID);
        targetWorldSize = d.targetWorldSize;
        spriteMaterial = FromGUID<Material>(d.materialGUID);
        if (spriteMaterial == null)
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        outputFolder = string.IsNullOrEmpty(d.outputFolder) ? "Assets/Prefabs/Obstacles" : d.outputFolder;
        prefabName = string.IsNullOrEmpty(d.prefabName) ? "Obstacle" : d.prefabName;
    }

    ConfigData CollectFieldsToConfig()
    {
        return new ConfigData
        {
            sourceSpriteGUID = ToGUID(sourceSprite),
            targetWorldSize = targetWorldSize,
            materialGUID = ToGUID(spriteMaterial),
            outputFolder = outputFolder,
            prefabName = prefabName
        };
    }

    // ──────────────────────────────────────────────
    //  UI
    // ──────────────────────────────────────────────

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("障碍物制造器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择 Sprite → 配置参数 → 生成障碍物预制体。\n" +
            "不修改 TextureImporter，尊重已有 Sprite Editor 切片。\n" +
            "碰撞器与图案大小一致。\n" +
            "配置自动保存，关闭 Unity 不丢失。", MessageType.Info);

        DrawPresetSection();

        // ── 输入 Sprite ──
        EditorGUILayout.Space();
        GUILayout.Label("输入 Sprite", EditorStyles.miniBoldLabel);

        // 拖放区域
        var dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, sourceSprite != null ? $"当前: {sourceSprite.name}" : "拖放 Sprite 到此处", GUI.skin.box);
        HandleDragDrop(dropArea);

        sourceSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", sourceSprite, typeof(Sprite), false);

        // ── 障碍物尺寸 ──
        EditorGUILayout.Space();
        GUILayout.Label("障碍物尺寸", EditorStyles.miniBoldLabel);
        targetWorldSize = EditorGUILayout.Vector2Field("目标世界尺寸 (宽,高)", targetWorldSize);

        if (sourceSprite != null)
        {
            Vector2 natSize = sourceSprite.bounds.size;
            float scaleX = natSize.x > 0 ? targetWorldSize.x / natSize.x : 1f;
            float scaleY = natSize.y > 0 ? targetWorldSize.y / natSize.y : 1f;
            EditorGUILayout.LabelField("Sprite 原始世界尺寸", $"{natSize.x:F2} × {natSize.y:F2}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("缩放比例", $"X={scaleX:F3}  Y={scaleY:F3}", EditorStyles.miniLabel);

            // 预览缩略图
            var previewTex = AssetPreview.GetAssetPreview(sourceSprite);
            if (previewTex != null)
            {
                GUILayout.Space(5);
                GUILayout.Label(previewTex);
            }
        }

        // ── 材质 ──
        EditorGUILayout.Space();
        GUILayout.Label("材质", EditorStyles.miniBoldLabel);
        spriteMaterial = (Material)EditorGUILayout.ObjectField("材质", spriteMaterial, typeof(Material), false);
        if (spriteMaterial == null)
            EditorGUILayout.HelpBox("未指定材质，将使用 Sprites/Default", MessageType.Warning);

        // ── 输出 ──
        EditorGUILayout.Space();
        GUILayout.Label("输出", EditorStyles.miniBoldLabel);
        prefabName = EditorGUILayout.TextField("预制体名称", prefabName);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("输出路径");
        outputFolder = EditorGUILayout.TextField(outputFolder);
        if (GUILayout.Button("浏览", GUILayout.Width(50)))
        {
            string absStart = Path.GetFullPath(outputFolder);
            if (!Directory.Exists(absStart)) absStart = Application.dataPath;
            string picked = EditorUtility.OpenFolderPanel("选择输出文件夹", absStart, "");
            if (!string.IsNullOrEmpty(picked))
            {
                string rel = FileUtil.GetProjectRelativePath(picked);
                if (!rel.StartsWith("Assets/")) { Warn("路径必须在 Assets/ 目录下"); }
                else outputFolder = rel.Replace('\\', '/');
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("完整路径", $"{outputFolder}/{prefabName}.prefab", EditorStyles.miniLabel);

        // ── 生成 ──
        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(sourceSprite == null || spriteMaterial == null))
        {
            if (GUILayout.Button("生成障碍物预制体", GUILayout.Height(30)))
            {
                SaveCurrentToPresets();
                SavePresetsToDisk();
                GenerateObstacle();
            }
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            SaveCurrentToPresets();
            SavePresetsToDisk();
        }
    }

    void DrawPresetSection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("预案", EditorStyles.miniBoldLabel);

        if (_renaming)
        {
            EditorGUILayout.BeginHorizontal();
            _renameBuffer = EditorGUILayout.TextField("编辑预案名", _renameBuffer);
            if (GUILayout.Button("确定", GUILayout.Width(40)))
            {
                var p = GetCurrentPreset();
                if (p != null && !string.IsNullOrWhiteSpace(_renameBuffer))
                {
                    _presets.currentName = _renameBuffer;
                    p.name = _renameBuffer;
                    SavePresetsToDisk();
                }
                _renaming = false;
            }
            if (GUILayout.Button("取消", GUILayout.Width(40)))
                _renaming = false;
            EditorGUILayout.EndHorizontal();
        }

        string[] names = _presets.presets.Select(p => p.name).ToArray();
        int curIdx = System.Array.IndexOf(names, _presets.currentName);
        if (curIdx < 0) curIdx = 0;

        EditorGUILayout.BeginHorizontal();
        int newIdx = EditorGUILayout.Popup("当前预案", curIdx, names);
        if (newIdx != curIdx)
        {
            SaveCurrentToPresets();
            _presets.currentName = names[newIdx];
            ApplyConfigToFields(GetCurrentPreset()?.config);
            SavePresetsToDisk();
        }

        if (GUILayout.Button("重命名", GUILayout.Width(60)))
        {
            var p = GetCurrentPreset();
            if (p != null)
            {
                _renameBuffer = p.name;
                _renaming = true;
            }
        }

        if (GUILayout.Button("删除", GUILayout.Width(50)))
        {
            if (_presets.presets.Count <= 1)
                Warn("至少保留一个预案");
            else
            {
                var p = GetCurrentPreset();
                if (p != null)
                {
                    _presets.presets.Remove(p);
                    _presets.currentName = _presets.presets[0].name;
                    ApplyConfigToFields(GetCurrentPreset()?.config);
                    SavePresetsToDisk();
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _newPresetName = EditorGUILayout.TextField("新预案名称", _newPresetName);
        if (GUILayout.Button("新建", GUILayout.Width(50)))
        {
            if (string.IsNullOrWhiteSpace(_newPresetName))
                Warn("请输入预案名称");
            else if (_presets.presets.Any(p => p.name == _newPresetName))
                Warn("预案名称已存在");
            else
            {
                SaveCurrentToPresets();
                _presets.presets.Add(new PresetData
                {
                    name = _newPresetName,
                    config = CollectFieldsToConfig()
                });
                _presets.currentName = _newPresetName;
                _newPresetName = "";
                SavePresetsToDisk();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // ──────────────────────────────────────────────
    //  拖放
    // ──────────────────────────────────────────────

    void HandleDragDrop(Rect dropArea)
    {
        var evt = Event.current;
        if (evt.type == EventType.DragUpdated && dropArea.Contains(evt.mousePosition))
        {
            bool hasSprite = DragAndDrop.objectReferences.OfType<Sprite>().Any();
            DragAndDrop.visualMode = hasSprite ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            Event.current.Use();
        }
        else if (evt.type == EventType.DragPerform && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.AcceptDrag();
            var sp = DragAndDrop.objectReferences.OfType<Sprite>().FirstOrDefault();
            if (sp != null)
                sourceSprite = sp;
            Event.current.Use();
        }
    }

    // ──────────────────────────────────────────────
    //  生成
    // ──────────────────────────────────────────────

    void GenerateObstacle()
    {
        if (sourceSprite == null) { Warn("请选择 Sprite"); return; }
        if (spriteMaterial == null) { Warn("请指定材质"); return; }

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string parent = Path.GetDirectoryName(outputFolder).Replace('\\', '/');
            string folderName = Path.GetFileName(outputFolder);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                Warn($"父文件夹不存在: {parent}");
                return;
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }

        EnsureTag("Obstacles");

        string prefabPath = $"{outputFolder}/{prefabName}.prefab";

        var go = new GameObject(prefabName);
        go.tag = "Obstacles";

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sourceSprite;
        sr.sharedMaterial = spriteMaterial;
        sr.sortingOrder = 0;
        sr.drawMode = SpriteDrawMode.Simple;

        // 缩放到目标世界尺寸
        Vector2 spriteWorldSize = sourceSprite.bounds.size;
        float sx = spriteWorldSize.x > 0 ? targetWorldSize.x / spriteWorldSize.x : 1f;
        float sy = spriteWorldSize.y > 0 ? targetWorldSize.y / spriteWorldSize.y : 1f;
        go.transform.localScale = new Vector3(sx, sy, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = spriteWorldSize;  // 局部空间=Sprite原始尺寸, 缩放后自动匹配图案

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ObstacleMaker] 生成: {prefabPath} | Sprite: {sourceSprite.name} | 原始: {spriteWorldSize} → 目标: {targetWorldSize}");
        EditorUtility.DisplayDialog("完成", $"障碍物预制体已生成:\n{prefabPath}", "确定");
    }

    // ──────────────────────────────────────────────
    //  工具
    // ──────────────────────────────────────────────

    static void EnsureTag(string tag)
    {
        var tags = UnityEditorInternal.InternalEditorUtility.tags;
        if (!tags.Contains(tag))
            UnityEditorInternal.InternalEditorUtility.AddTag(tag);
    }

    static void Warn(string msg) => EditorUtility.DisplayDialog("提示", msg, "确定");
}
