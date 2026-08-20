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

    // === 障碍物类型 ===
    enum ObstacleType { Normal, HoleTrap, SpikeTrap }
    ObstacleType _obstacleType = ObstacleType.Normal;

    // === 洞陷阱参数 ===
    float _damageRatio = 0.1f;
    float _trapDuration = 2f;
    float _shrinkScale = 0.2f;
    float _popOffset = 1.5f;
    Vector2 _colliderSize = new Vector2(1f, 1f);

    // === 刺陷阱参数 ===
    Sprite _spikeSprite1;
    Sprite _spikeSprite2;
    Sprite _spikeSprite3;
    float _spikeDamageRatio = 0.1f;
    float _spikeDamageCooldown = 0.5f;
    float _spikeState1End = 1f;
    float _spikeState2FirstEnd = 1.5f;
    float _spikeState3End = 3f;
    float _spikeState2SecondEnd = 3.5f;

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
        public int obstacleType = 0;
        public float damageRatio = 0.1f;
        public float trapDuration = 2f;
        public float shrinkScale = 0.2f;
        public float popOffset = 1.5f;
        public Vector2 colliderSize = new Vector2(1f, 1f);
        // Spike trap
        public string spikeSprite1GUID = "";
        public string spikeSprite2GUID = "";
        public string spikeSprite3GUID = "";
        public float spikeDamageRatio = 0.1f;
        public float spikeDamageCooldown = 0.5f;
        public float spikeState1End = 1f;
        public float spikeState2FirstEnd = 1.5f;
        public float spikeState3End = 3f;
        public float spikeState2SecondEnd = 3.5f;
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
        _obstacleType = (ObstacleType)d.obstacleType;
        _damageRatio = d.damageRatio > 0f ? d.damageRatio : 0.1f;
        _trapDuration = d.trapDuration > 0f ? d.trapDuration : 2f;
        _shrinkScale = d.shrinkScale > 0f ? d.shrinkScale : 0.2f;
        _popOffset = d.popOffset > 0f ? d.popOffset : 1.5f;
        _colliderSize = d.colliderSize.magnitude > 0f ? d.colliderSize : new Vector2(1f, 1f);
        _spikeSprite1 = FromGUID<Sprite>(d.spikeSprite1GUID);
        _spikeSprite2 = FromGUID<Sprite>(d.spikeSprite2GUID);
        _spikeSprite3 = FromGUID<Sprite>(d.spikeSprite3GUID);
        _spikeDamageRatio = d.spikeDamageRatio > 0f ? d.spikeDamageRatio : 0.1f;
        _spikeDamageCooldown = d.spikeDamageCooldown > 0f ? d.spikeDamageCooldown : 0.5f;
        _spikeState1End = d.spikeState1End > 0f ? d.spikeState1End : 1f;
        _spikeState2FirstEnd = d.spikeState2FirstEnd > 0f ? d.spikeState2FirstEnd : 1.5f;
        _spikeState3End = d.spikeState3End > 0f ? d.spikeState3End : 3f;
        _spikeState2SecondEnd = d.spikeState2SecondEnd > 0f ? d.spikeState2SecondEnd : 3.5f;
    }

    ConfigData CollectFieldsToConfig()
    {
        return new ConfigData
        {
            sourceSpriteGUID = ToGUID(sourceSprite),
            targetWorldSize = targetWorldSize,
            materialGUID = ToGUID(spriteMaterial),
            outputFolder = outputFolder,
            prefabName = prefabName,
            obstacleType = (int)_obstacleType,
            damageRatio = _damageRatio,
            trapDuration = _trapDuration,
            shrinkScale = _shrinkScale,
            popOffset = _popOffset,
            colliderSize = _colliderSize,
            spikeSprite1GUID = ToGUID(_spikeSprite1),
            spikeSprite2GUID = ToGUID(_spikeSprite2),
            spikeSprite3GUID = ToGUID(_spikeSprite3),
            spikeDamageRatio = _spikeDamageRatio,
            spikeDamageCooldown = _spikeDamageCooldown,
            spikeState1End = _spikeState1End,
            spikeState2FirstEnd = _spikeState2FirstEnd,
            spikeState3End = _spikeState3End,
            spikeState2SecondEnd = _spikeState2SecondEnd
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

        // ── 障碍物类型 ──
        EditorGUILayout.Space();
        GUILayout.Label("障碍物类型", EditorStyles.miniBoldLabel);
        _obstacleType = (ObstacleType)GUILayout.SelectionGrid(
            (int)_obstacleType, new[] { "普通障碍物", "洞陷阱", "刺陷阱" }, 3, GUILayout.Height(25));

        // ── 洞陷阱参数 ──
        if (_obstacleType == ObstacleType.HoleTrap)
        {
            EditorGUILayout.Space();
            GUILayout.Label("陷阱参数", EditorStyles.miniBoldLabel);
            _damageRatio = EditorGUILayout.Slider("扣血比例 (占最大生命)", _damageRatio, 0f, 1f);
            _trapDuration = EditorGUILayout.FloatField("陷入时间 (秒)", _trapDuration);
            _shrinkScale = EditorGUILayout.Slider("缩小倍率", _shrinkScale, 0.05f, 1f);
            _popOffset = EditorGUILayout.FloatField("弹出距离", _popOffset);
            _colliderSize = EditorGUILayout.Vector2Field("碰撞箱尺寸 (宽,高)", _colliderSize);
        }

        // ── 刺陷阱参数 ──
        if (_obstacleType == ObstacleType.SpikeTrap)
        {
            EditorGUILayout.Space();
            GUILayout.Label("刺陷阱参数", EditorStyles.miniBoldLabel);
            _spikeSprite1 = (Sprite)EditorGUILayout.ObjectField("状态1 Sprite (未探出)", _spikeSprite1, typeof(Sprite), false);
            _spikeSprite2 = (Sprite)EditorGUILayout.ObjectField("状态2 Sprite (将探出)", _spikeSprite2, typeof(Sprite), false);
            _spikeSprite3 = (Sprite)EditorGUILayout.ObjectField("状态3 Sprite (完全探出)", _spikeSprite3, typeof(Sprite), false);
            EditorGUILayout.Space();
            _spikeDamageRatio = EditorGUILayout.Slider("扣血比例 (占最大生命)", _spikeDamageRatio, 0f, 1f);
            _spikeDamageCooldown = EditorGUILayout.FloatField("伤害冷却 (秒)", _spikeDamageCooldown);
            EditorGUILayout.Space();
            GUILayout.Label("时序 (秒)", EditorStyles.miniBoldLabel);
            _spikeState1End = EditorGUILayout.FloatField("状态1结束", _spikeState1End);
            _spikeState2FirstEnd = EditorGUILayout.FloatField("状态2首段结束", _spikeState2FirstEnd);
            _spikeState3End = EditorGUILayout.FloatField("状态3结束", _spikeState3End);
            _spikeState2SecondEnd = EditorGUILayout.FloatField("状态2次段结束", _spikeState2SecondEnd);
        }

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
            string btnLabel = _obstacleType switch
            {
                ObstacleType.HoleTrap => "生成洞陷阱预制体",
                ObstacleType.SpikeTrap => "生成刺陷阱预制体",
                _ => "生成障碍物预制体"
            };
            if (GUILayout.Button(btnLabel, GUILayout.Height(30)))
            {
                SaveCurrentToPresets();
                SavePresetsToDisk();
                switch (_obstacleType)
                {
                    case ObstacleType.HoleTrap: GenerateHoleTrap(); break;
                    case ObstacleType.SpikeTrap: GenerateSpikeTrap(); break;
                    default: GenerateObstacle(); break;
                }
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

    void GenerateHoleTrap()
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

        EnsureTag("Hole");

        string prefabPath = $"{outputFolder}/{prefabName}.prefab";

        var go = new GameObject(prefabName);
        go.tag = "Hole";

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

        // 碰撞器：isTrigger + 指定尺寸
        var col = go.AddComponent<BoxCollider2D>();
        col.size = _colliderSize;
        col.isTrigger = true;

        // 添加 HoleTrap 组件并配置参数
        var trap = go.AddComponent<HoleTrap>();
        trap.Configure(_damageRatio, _trapDuration, _shrinkScale, _popOffset);

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ObstacleMaker] 生成洞陷阱: {prefabPath} | Sprite: {sourceSprite.name} | 碰撞箱尺寸: {_colliderSize} | 扣血: {_damageRatio} | 陷入: {_trapDuration}s | 缩小: {_shrinkScale}");
        EditorUtility.DisplayDialog("完成", $"洞陷阱预制体已生成:\n{prefabPath}", "确定");
    }

    void GenerateSpikeTrap()
    {
        if (_spikeSprite1 == null || _spikeSprite2 == null || _spikeSprite3 == null)
        { Warn("请指定三张状态 Sprite"); return; }
        if (spriteMaterial == null) { Warn("请指定材质"); return; }

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string parent = Path.GetDirectoryName(outputFolder).Replace('\\', '/');
            string folderName = Path.GetFileName(outputFolder);
            if (!AssetDatabase.IsValidFolder(parent))
            { Warn($"父文件夹不存在: {parent}"); return; }
            AssetDatabase.CreateFolder(parent, folderName);
        }

        EnsureTag("Spike");

        string prefabPath = $"{outputFolder}/{prefabName}.prefab";

        var go = new GameObject(prefabName);
        go.tag = "Spike";

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _spikeSprite1;
        sr.sharedMaterial = spriteMaterial;
        sr.sortingOrder = 0;

        // 缩放到目标世界尺寸
        Vector2 spriteWorldSize = _spikeSprite1.bounds.size;
        float sx = spriteWorldSize.x > 0 ? targetWorldSize.x / spriteWorldSize.x : 1f;
        float sy = spriteWorldSize.y > 0 ? targetWorldSize.y / spriteWorldSize.y : 1f;
        go.transform.localScale = new Vector3(sx, sy, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = spriteWorldSize;
        col.isTrigger = true;

        var spike = go.AddComponent<NerveSpike>();
        var so = new SerializedObject(spike);
        so.FindProperty("_state1Sprite").objectReferenceValue = _spikeSprite1;
        so.FindProperty("_state2Sprite").objectReferenceValue = _spikeSprite2;
        so.FindProperty("_state3Sprite").objectReferenceValue = _spikeSprite3;
        so.FindProperty("_state1End").floatValue = _spikeState1End;
        so.FindProperty("_state2FirstEnd").floatValue = _spikeState2FirstEnd;
        so.FindProperty("_state3End").floatValue = _spikeState3End;
        so.FindProperty("_state2SecondEnd").floatValue = _spikeState2SecondEnd;
        so.FindProperty("_damageRatio").floatValue = _spikeDamageRatio;
        so.FindProperty("_damageCooldown").floatValue = _spikeDamageCooldown;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ObstacleMaker] 生成刺陷阱: {prefabPath} | spriteSize={spriteWorldSize} | 扣血: {_spikeDamageRatio*100}% | 时序: {_spikeState1End}/{_spikeState2FirstEnd}/{_spikeState3End}/{_spikeState2SecondEnd}");
        EditorUtility.DisplayDialog("完成", $"刺陷阱预制体已生成:\n{prefabPath}", "确定");
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
