using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 房间搭建工具 V3 — 墙体2×2格子，单方向旋转，多类型随机。
/// 菜单: Tools > 搭建房间 (2×2墙体版)
/// 与 V2 区别：
/// 1. 墙壁/墙角/墙体延伸的格子为 wallTileSize×wallTileSize（默认2×2），地板仍为1×1
/// 2. 房间宽高必须为 wallTileSize 的整数倍（偶数），构建时强制校验
/// 3. 门默认缩放为 wallTileSize，以匹配墙体开口
/// 4. 墙体步进以 wallTileSize 为单位，每个墙块覆盖 2×2 格子区域
/// </summary>
public class RoomBuilderTool3 : EditorWindow
{
    const string PresetsPrefsKey = "RoomBuilderTool3_Presets";

    // === 格子与房间 ===
    float tileSize = 1f;
    int wallTileSize = 2;           // 墙体格子边长（以 tileSize 为单位），默认2
    int roomTilesX = 20;
    int roomTilesY = 12;

    // === 地板 (1×1, 多类型随机) ===
    enum FloorMode { Tiled, Single }
    FloorMode floorMode = FloorMode.Single;
    List<Sprite> floorSprites = new();
    Vector2 floorScale = new(1f, 1f), floorOffset = Vector2.zero;

    // === 墙壁 (wallTileSize×wallTileSize, 单方向+旋转+多类型随机) ===
    List<Sprite> wallSprites = new();
    DoorDirection wallSourceDir = DoorDirection.Top;
    Vector2 wallScale = new(1f, 1f);    // (X=沿墙方向, Y=垂直墙方向)
    Vector2 wallOffset = Vector2.zero;  // (X=沿墙方向, Y=垂直墙向外)
    WallPlaceMode wallPlaceMode = WallPlaceMode.Random;
    int wallStartIndexTop = 0;
    int wallStartIndexBottom = 0;
    int wallStartIndexLeft = 0;
    int wallStartIndexRight = 0;

    // === 墙体延伸 (wallTileSize×wallTileSize, 单方向+旋转+多类型随机, 纯装饰) ===
    List<Sprite> wallExtOutSprites = new();
    List<Sprite> wallExtInSprites = new();
    DoorDirection wallExtSourceDir = DoorDirection.Top;
    Vector2 wallExtScale = new(1f, 1f);
    Vector2 wallExtOffset = Vector2.zero;

    // === 墙角 (wallTileSize×wallTileSize, 多类型随机, 单配置+flip) ===
    enum CornerDir { TopLeft, TopRight, BottomRight, BottomLeft }
    enum WallPlaceMode { Random, Clockwise }
    List<Sprite> cornerSprites = new();
    CornerDir cornerDefaultDir = CornerDir.TopLeft;
    Vector2 cornerScale = new(1f, 1f);
    Vector2 cornerOffset = Vector2.zero;

    // === 门 ===
    Sprite doorSprite;
    DoorDirection doorSpriteDefaultDir = DoorDirection.Right;
    Vector2 doorScale = new(2f, 2f);   // 默认匹配墙体2×2
    Vector2 doorTopOffset = Vector2.zero, doorBottomOffset = Vector2.zero;
    Vector2 doorLeftOffset = Vector2.zero, doorRightOffset = Vector2.zero;

    // === RoomRoot 数据 ===
    int enemySpawnCount = 8;
    int itemSpawnCount = 5;

    // === 材质 ===
    Material spriteMaterial;

    // === 输出 ===
    string prefabName = "Room_Normal_1F";
    string outputFolder = "Assets/Prefabs/Rooms";

    // === 便捷属性 ===
    float WallWorld => wallTileSize * tileSize;
    float HalfWall => WallWorld * 0.5f;

    // === 预案系统 ===
    [System.Serializable]
    class ConfigData
    {
        public float tileSize = 1f;
        public int wallTileSize = 2;
        public int roomTilesX = 20, roomTilesY = 12;
        public int floorMode = 1;
        public List<string> floorSpriteGUIDs = new();
        public Vector2 floorScale = new(1f, 1f), floorOffset = Vector2.zero;

        public List<string> wallSpriteGUIDs = new();
        public int wallSourceDir = 1;
        public Vector2 wallScale = new(1f, 1f), wallOffset = Vector2.zero;

        public int wallPlaceMode = 0;
        public int wallStartIndexTop = 0;
        public int wallStartIndexBottom = 0;
        public int wallStartIndexLeft = 0;
        public int wallStartIndexRight = 0;

        public List<string> wallExtOutGUIDs = new();
        public List<string> wallExtInGUIDs = new();
        public int wallExtSourceDir = 1;
        public Vector2 wallExtScale = new(1f, 1f), wallExtOffset = Vector2.zero;

        public List<string> cornerSpriteGUIDs = new();
        public int cornerDefaultDir = 0;
        public Vector2 cornerScale = new(1f, 1f), cornerOffset = Vector2.zero;

        public string doorSpriteGUID;
        public int doorSpriteDefaultDir;
        public Vector2 doorScale = new(2f, 2f);
        public Vector2 doorTopOffset = Vector2.zero, doorBottomOffset = Vector2.zero;
        public Vector2 doorLeftOffset = Vector2.zero, doorRightOffset = Vector2.zero;

        public int enemySpawnCount = 8, itemSpawnCount = 5;
        public string materialGUID;
        public string prefabName = "Room_Normal_1F";
        public string outputFolder = "Assets/Prefabs/Rooms";
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

    [MenuItem("Tools/搭建房间 (2×2墙体版)")]
    static void ShowWindow() => GetWindow<RoomBuilderTool3>("房间搭建工具 (2×2墙体版)");

    void OnEnable() => LoadPresets();

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

    static List<string> ToGUIDList(List<Sprite> sprites)
    {
        var result = new List<string>();
        foreach (var s in sprites)
            result.Add(s == null ? "" : ToGUID(s));
        return result;
    }

    static List<Sprite> FromGUIDList(List<string> guids)
    {
        var result = new List<Sprite>();
        if (guids == null) return result;
        foreach (var g in guids)
            result.Add(FromGUID<Sprite>(g));
        return result;
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
        tileSize = d.tileSize;
        wallTileSize = d.wallTileSize;
        roomTilesX = d.roomTilesX;
        roomTilesY = d.roomTilesY;
        floorMode = (FloorMode)d.floorMode;
        floorSprites = FromGUIDList(d.floorSpriteGUIDs);
        floorScale = d.floorScale;
        floorOffset = d.floorOffset;

        wallSprites = FromGUIDList(d.wallSpriteGUIDs);
        wallSourceDir = (DoorDirection)d.wallSourceDir;
        wallScale = d.wallScale;
        wallOffset = d.wallOffset;
        wallPlaceMode = (WallPlaceMode)d.wallPlaceMode;
        wallStartIndexTop = d.wallStartIndexTop;
        wallStartIndexBottom = d.wallStartIndexBottom;
        wallStartIndexLeft = d.wallStartIndexLeft;
        wallStartIndexRight = d.wallStartIndexRight;

        wallExtOutSprites = FromGUIDList(d.wallExtOutGUIDs);
        wallExtInSprites = FromGUIDList(d.wallExtInGUIDs);
        wallExtSourceDir = (DoorDirection)d.wallExtSourceDir;
        wallExtScale = d.wallExtScale;
        wallExtOffset = d.wallExtOffset;

        cornerSprites = FromGUIDList(d.cornerSpriteGUIDs);
        cornerDefaultDir = (CornerDir)d.cornerDefaultDir;
        cornerScale = d.cornerScale;
        cornerOffset = d.cornerOffset;

        doorSprite = FromGUID<Sprite>(d.doorSpriteGUID);
        doorSpriteDefaultDir = (DoorDirection)d.doorSpriteDefaultDir;
        doorScale = d.doorScale;
        doorTopOffset = d.doorTopOffset; doorBottomOffset = d.doorBottomOffset;
        doorLeftOffset = d.doorLeftOffset; doorRightOffset = d.doorRightOffset;

        enemySpawnCount = d.enemySpawnCount;
        itemSpawnCount = d.itemSpawnCount;
        spriteMaterial = FromGUID<Material>(d.materialGUID);
        if (spriteMaterial == null)
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        prefabName = d.prefabName;
        outputFolder = string.IsNullOrEmpty(d.outputFolder) ? "Assets/Prefabs/Rooms" : d.outputFolder;
    }

    ConfigData CollectFieldsToConfig()
    {
        return new ConfigData
        {
            tileSize = tileSize,
            wallTileSize = wallTileSize,
            roomTilesX = roomTilesX,
            roomTilesY = roomTilesY,
            floorMode = (int)floorMode,
            floorSpriteGUIDs = ToGUIDList(floorSprites),
            floorScale = floorScale,
            floorOffset = floorOffset,
            wallSpriteGUIDs = ToGUIDList(wallSprites),
            wallSourceDir = (int)wallSourceDir,
            wallScale = wallScale,
            wallOffset = wallOffset,
            wallPlaceMode = (int)wallPlaceMode,
            wallStartIndexTop = wallStartIndexTop,
            wallStartIndexBottom = wallStartIndexBottom,
            wallStartIndexLeft = wallStartIndexLeft,
            wallStartIndexRight = wallStartIndexRight,
            wallExtOutGUIDs = ToGUIDList(wallExtOutSprites),
            wallExtInGUIDs = ToGUIDList(wallExtInSprites),
            wallExtSourceDir = (int)wallExtSourceDir,
            wallExtScale = wallExtScale,
            wallExtOffset = wallExtOffset,
            cornerSpriteGUIDs = ToGUIDList(cornerSprites),
            cornerDefaultDir = (int)cornerDefaultDir,
            cornerScale = cornerScale,
            cornerOffset = cornerOffset,
            doorSpriteGUID = ToGUID(doorSprite),
            doorSpriteDefaultDir = (int)doorSpriteDefaultDir,
            doorScale = doorScale,
            doorTopOffset = doorTopOffset, doorBottomOffset = doorBottomOffset,
            doorLeftOffset = doorLeftOffset, doorRightOffset = doorRightOffset,
            enemySpawnCount = enemySpawnCount,
            itemSpawnCount = itemSpawnCount,
            materialGUID = ToGUID(spriteMaterial),
            prefabName = prefabName,
            outputFolder = outputFolder
        };
    }

    // ──────────────────────────────────────────────
    //  SpritePicker — 按模式选取素材
    // ──────────────────────────────────────────────

    class SpritePicker
    {
        readonly List<Sprite> _valid;
        readonly List<int> _order;
        int _cursor;
        readonly bool _random;

        public SpritePicker(List<Sprite> sprites, WallPlaceMode mode, int startIndex = 0)
        {
            _valid = sprites?.Where(s => s != null).ToList() ?? new List<Sprite>();
            _random = mode == WallPlaceMode.Random;
            if (!_random && _valid.Count > 0)
            {
                _order = Enumerable.Range(0, _valid.Count).ToList();
                _cursor = startIndex % _order.Count;
            }
        }

        public bool IsEmpty => _valid.Count == 0;

        public Sprite Next()
        {
            if (_valid.Count == 0) return null;
            if (_random) return _valid[Random.Range(0, _valid.Count)];
            return _valid[_order[_cursor++ % _order.Count]];
        }
    }

    // ──────────────────────────────────────────────
    //  UI
    // ──────────────────────────────────────────────

    Vector2 _scroll;

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("房间搭建工具 (2×2墙体版)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "自动平铺地板/墙壁/门，生成房间预制体。\n" +
            "墙壁/墙角/墙体延伸为 wallTileSize×wallTileSize 格子（默认2×2），地板仍为1×1。\n" +
            "房间宽高必须为 wallTileSize 的整数倍。\n" +
            "墙壁只需配置一个方向，通过旋转生成其他三个方向。\n" +
            "墙壁/墙角/延伸支持多种素材，随机/顺时针有序两种布置模式可选。\n" +
            "配置自动保存，关闭 Unity 不丢失。", MessageType.Info);

        DrawPresetSection();

        // ── 格子与房间 ──
        EditorGUILayout.Space();
        GUILayout.Label("格子 & 房间", EditorStyles.miniBoldLabel);
        tileSize = EditorGUILayout.FloatField("格子大小 (世界单位)", tileSize);
        wallTileSize = EditorGUILayout.IntSlider("墙体格子边长 (格)", wallTileSize, 1, 4);
        EditorGUI.BeginChangeCheck();
        roomTilesX = EditorGUILayout.IntField("房间宽 (格子数)", roomTilesX);
        roomTilesY = EditorGUILayout.IntField("房间高 (格子数)", roomTilesY);
        if (EditorGUI.EndChangeCheck())
        {
            // 强制对齐到 wallTileSize 的整数倍
            roomTilesX = Mathf.Max(wallTileSize, Mathf.CeilToInt(roomTilesX / (float)wallTileSize) * wallTileSize);
            roomTilesY = Mathf.Max(wallTileSize, Mathf.CeilToInt(roomTilesY / (float)wallTileSize) * wallTileSize);
        }

        // 偶数校验提示
        bool roomValid = roomTilesX % wallTileSize == 0 && roomTilesY % wallTileSize == 0;
        if (!roomValid)
            EditorGUILayout.HelpBox(
                $"房间宽高必须为 {wallTileSize} 的整数倍！当前: {roomTilesX}×{roomTilesY}", MessageType.Error);
        else
            EditorGUILayout.HelpBox(
                $"每面墙块数: {roomTilesX / wallTileSize}×{roomTilesY / wallTileSize} = " +
                $"{roomTilesX / wallTileSize + roomTilesY / wallTileSize} 块/方向", MessageType.None);

        // ── 地板 ──
        EditorGUILayout.Space();
        GUILayout.Label("地板 (1×1, 多类型随机)", EditorStyles.miniBoldLabel);
        floorMode = (FloorMode)EditorGUILayout.EnumPopup("地板模式", floorMode);
        DrawSpriteList("地板素材列表", floorSprites);
        floorScale = EditorGUILayout.Vector2Field("地板缩放 (X=横向, Y=纵向)", floorScale);
        floorOffset = EditorGUILayout.Vector2Field("地板偏移 (X=横向, Y=纵向)", floorOffset);

        // ── 墙壁 ──
        EditorGUILayout.Space();
        GUILayout.Label($"墙壁 ({wallTileSize}×{wallTileSize}, 单方向+旋转, 多类型)", EditorStyles.miniBoldLabel);
        wallSourceDir = (DoorDirection)EditorGUILayout.EnumPopup("素材默认朝向", wallSourceDir);
        DrawSpriteList("墙壁素材列表", wallSprites);
        wallPlaceMode = (WallPlaceMode)EditorGUILayout.EnumPopup("墙壁布置模式", wallPlaceMode);
        if (wallPlaceMode == WallPlaceMode.Clockwise)
        {
            wallStartIndexTop = EditorGUILayout.IntField("上墙起始索引", wallStartIndexTop);
            wallStartIndexBottom = EditorGUILayout.IntField("下墙起始索引", wallStartIndexBottom);
            wallStartIndexLeft = EditorGUILayout.IntField("左墙起始索引", wallStartIndexLeft);
            wallStartIndexRight = EditorGUILayout.IntField("右墙起始索引", wallStartIndexRight);
        }
        wallScale = EditorGUILayout.Vector2Field("缩放 (X=沿墙, Y=垂直墙)", wallScale);
        wallOffset = EditorGUILayout.Vector2Field("偏移 (X=沿墙, Y=垂直墙向外)", wallOffset);

        // ── 墙体延伸 ──
        EditorGUILayout.Space();
        GUILayout.Label($"墙体延伸 (可选, {wallTileSize}×{wallTileSize}, 纯装饰, 单方向+旋转)", EditorStyles.miniBoldLabel);
        wallExtSourceDir = (DoorDirection)EditorGUILayout.EnumPopup("延伸素材默认朝向", wallExtSourceDir);
        DrawSpriteList("外侧延伸素材", wallExtOutSprites);
        DrawSpriteList("内侧延伸素材", wallExtInSprites);
        wallExtScale = EditorGUILayout.Vector2Field("延伸缩放 (X=沿墙, Y=垂直墙)", wallExtScale);
        wallExtOffset = EditorGUILayout.Vector2Field("延伸偏移 (X=沿墙, Y=垂直墙向外)", wallExtOffset);

        // ── 墙角 ──
        EditorGUILayout.Space();
        GUILayout.Label($"墙角 ({wallTileSize}×{wallTileSize}, 多类型随机, 单配置+翻转)", EditorStyles.miniBoldLabel);
        cornerDefaultDir = (CornerDir)EditorGUILayout.EnumPopup("素材默认朝向", cornerDefaultDir);
        DrawSpriteList("墙角素材列表", cornerSprites);
        cornerScale = EditorGUILayout.Vector2Field("墙角缩放 (X=横向, Y=纵向)", cornerScale);
        cornerOffset = EditorGUILayout.Vector2Field("墙角偏移 (X=横向, Y=纵向)", cornerOffset);

        // ── 门 ──
        EditorGUILayout.Space();
        GUILayout.Label("门", EditorStyles.miniBoldLabel);
        doorSprite = (Sprite)EditorGUILayout.ObjectField("门素材", doorSprite, typeof(Sprite), false);
        doorSpriteDefaultDir = (DoorDirection)EditorGUILayout.EnumPopup("门素材默认朝向", doorSpriteDefaultDir);
        doorScale = EditorGUILayout.Vector2Field("门缩放 (X=横向, Y=纵向)", doorScale);
        doorTopOffset = EditorGUILayout.Vector2Field("上门偏移 (X=横向, Y=纵向)", doorTopOffset);
        doorBottomOffset = EditorGUILayout.Vector2Field("下门偏移 (X=横向, Y=纵向)", doorBottomOffset);
        doorLeftOffset = EditorGUILayout.Vector2Field("左门偏移 (X=横向, Y=纵向)", doorLeftOffset);
        doorRightOffset = EditorGUILayout.Vector2Field("右门偏移 (X=横向, Y=纵向)", doorRightOffset);

        // ── 材质 & RoomRoot ──
        EditorGUILayout.Space();
        GUILayout.Label("材质 & RoomRoot", EditorStyles.miniBoldLabel);
        spriteMaterial = (Material)EditorGUILayout.ObjectField("材质", spriteMaterial, typeof(Material), false);
        enemySpawnCount = EditorGUILayout.IntSlider("敌人生成点数量", enemySpawnCount, 0, 20);
        itemSpawnCount = EditorGUILayout.IntSlider("道具生成点数量", itemSpawnCount, 0, 20);
        EditorGUILayout.LabelField("房间边界 (roomSize)", RoomSize.ToString("F1"));
        EditorGUILayout.LabelField("墙体世界尺寸", $"{WallWorld}×{WallWorld}");

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

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(!roomValid))
        {
            if (GUILayout.Button("生成房间预制体", GUILayout.Height(30)))
            {
                SaveCurrentToPresets();
                SavePresetsToDisk();
                BuildRoom();
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

    void DrawSpriteList(string label, List<Sprite> list)
    {
        EditorGUILayout.LabelField(label);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            list[i] = (Sprite)EditorGUILayout.ObjectField(list[i], typeof(Sprite), false);
            if (GUILayout.Button("删", GUILayout.Width(30)))
                list.RemoveAt(i);
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ 添加素材", GUILayout.Width(100)))
            list.Add(null);
    }

    // ──────────────────────────────────────────────
    //  构建
    // ──────────────────────────────────────────────

    Vector2 RoomSize => new(roomTilesX * tileSize, roomTilesY * tileSize);

    void BuildRoom()
    {
        if (spriteMaterial == null) { Warn("请指定材质"); return; }
        if (floorSprites.Count == 0 || floorSprites.All(s => s == null)) { Warn("请添加至少一个地板素材"); return; }
        if (doorSprite == null) { Warn("请指定门素材"); return; }
        if (wallSprites.Count == 0 || wallSprites.All(s => s == null)) { Warn("至少添加一个墙壁素材"); return; }
        if (roomTilesX % wallTileSize != 0 || roomTilesY % wallTileSize != 0)
        { Warn($"房间宽高必须为 {wallTileSize} 的整数倍"); return; }

        string prefabPath = $"{outputFolder}/{prefabName}.prefab";
        EnsureDir(prefabPath);

        var root = new GameObject(prefabName);
        var roomRoot = root.AddComponent<RoomRoot>();
        roomRoot.roomSize = RoomSize;

        // 地板
        if (floorMode == FloorMode.Single)
        {
            // 地板撑开到内墙壁边缘，随机选择素材
            Sprite picked = PickFloorSprite();
            Vector2 floorWorldSize = new(RoomSize.x, RoomSize.y);
            CreateSprite("Floor", picked, floorWorldSize, -2, root.transform,
                new Vector3(floorOffset.x, floorOffset.y, 0), floorScale, 0f);
        }
        else
            BuildTiledFloor(root.transform);

        // 墙壁
        var wallsGO = new GameObject("Walls");
        wallsGO.transform.SetParent(root.transform, false);
        BuildWalls(wallsGO.transform);

        // 门
        var doorsGO = new GameObject("Doors");
        doorsGO.transform.SetParent(root.transform, false);
        BuildDoor(doorsGO.transform, roomRoot, "Door_Top", DoorDirection.Top,
            new Vector3(0, roomTilesY * tileSize * 0.5f, 0), doorTopOffset);
        BuildDoor(doorsGO.transform, roomRoot, "Door_Bottom", DoorDirection.Bottom,
            new Vector3(0, -roomTilesY * tileSize * 0.5f, 0), doorBottomOffset);
        BuildDoor(doorsGO.transform, roomRoot, "Door_Left", DoorDirection.Left,
            new Vector3(-roomTilesX * tileSize * 0.5f, 0, 0), doorLeftOffset);
        BuildDoor(doorsGO.transform, roomRoot, "Door_Right", DoorDirection.Right,
            new Vector3(roomTilesX * tileSize * 0.5f, 0, 0), doorRightOffset);

        BuildSpawnPoints(root.transform, roomRoot);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RoomBuilder3] 已生成: {prefabPath}");
        EditorUtility.DisplayDialog("完成", $"房间预制体已生成:\n{prefabPath}", "确定");
    }

    // ──────────────────────────────────────────────
    //  地板 (1×1)
    // ──────────────────────────────────────────────

    void BuildTiledFloor(Transform parent)
    {
        var container = new GameObject("Floor").transform;
        container.SetParent(parent, false);
        container.localPosition = Vector3.zero;

        float halfW = roomTilesX * tileSize * 0.5f;
        float halfH = roomTilesY * tileSize * 0.5f;
        // 多铺一圈墙体厚度
        int ext = wallTileSize;

        for (int y = -ext; y < roomTilesY + ext; y++)
        {
            for (int x = -ext; x < roomTilesX + ext; x++)
            {
                float px = -halfW + (x + 0.5f) * tileSize;
                float py = -halfH + (y + 0.5f) * tileSize;
                Sprite picked = PickFloorSprite();
                CreateSprite($"Floor_{x}_{y}", picked,
                    new Vector2(tileSize, tileSize), -2, container,
                    new Vector3(px + floorOffset.x, py + floorOffset.y, 0), floorScale, 0f);
            }
        }
    }

    // ──────────────────────────────────────────────
    //  墙壁 (wallTileSize×wallTileSize, 单方向+旋转, 随机/顺时针有序)
    // ──────────────────────────────────────────────

    void BuildWalls(Transform container)
    {
        float halfW = roomTilesX * tileSize * 0.5f;
        float halfH = roomTilesY * tileSize * 0.5f;

        BuildWallForDirection(container, DoorDirection.Top, halfW, halfH);
        BuildWallForDirection(container, DoorDirection.Bottom, halfW, halfH);
        BuildWallForDirection(container, DoorDirection.Left, halfW, halfH);
        BuildWallForDirection(container, DoorDirection.Right, halfW, halfH);

        // 墙角（4个角共用一个 picker）
        var cornerPicker = new SpritePicker(cornerSprites, wallPlaceMode, 0);
        BuildCorner(container, "Corner_TL", new Vector3(-halfW - HalfWall, halfH + HalfWall, 0), CornerDir.TopLeft, cornerPicker);
        BuildCorner(container, "Corner_TR", new Vector3(halfW + HalfWall, halfH + HalfWall, 0), CornerDir.TopRight, cornerPicker);
        BuildCorner(container, "Corner_BR", new Vector3(halfW + HalfWall, -halfH - HalfWall, 0), CornerDir.BottomRight, cornerPicker);
        BuildCorner(container, "Corner_BL", new Vector3(-halfW - HalfWall, -halfH - HalfWall, 0), CornerDir.BottomLeft, cornerPicker);

        BuildWallExtensions(container, halfW, halfH);
    }

    /// <summary>
    /// 为指定方向构建墙壁：每个墙块为 wallTileSize×wallTileSize，步进 WallWorld，按布置模式选取素材
    /// </summary>
    void BuildWallForDirection(Transform container, DoorDirection targetDir, float halfW, float halfH)
    {
        var picker = new SpritePicker(wallSprites, wallPlaceMode, GetWallStartIndex(targetDir));
        if (picker.IsEmpty) return;

        bool horizontal = targetDir == DoorDirection.Top || targetDir == DoorDirection.Bottom;
        float rotation = DirToAngle(targetDir) - DirToAngle(wallSourceDir);

        float start, end, fixedPos;
        string prefix = $"Wall_{targetDir}";

        if (horizontal)
        {
            start = -halfW;
            end = halfW;
            fixedPos = (targetDir == DoorDirection.Top) ? halfH + HalfWall : -halfH - HalfWall;
        }
        else
        {
            start = -halfH;
            end = halfH;
            fixedPos = (targetDir == DoorDirection.Right) ? halfW + HalfWall : -halfW - HalfWall;
        }

        Vector2 worldOffset = RotateVector2(wallOffset, rotation);

        // 从第一个墙块中心开始，步进 WallWorld
        float pos = start + HalfWall;
        int i = 0;
        while (pos < end - 0.001f)
        {
            Vector3 localPos = horizontal
                ? new Vector3(pos, fixedPos, 0)
                : new Vector3(fixedPos, pos, 0);
            localPos += new Vector3(worldOffset.x, worldOffset.y, 0);

            Sprite sprite = picker.Next();
            var go = CreateSprite($"{prefix}_{i++}", sprite,
                new Vector2(WallWorld, WallWorld), 0, container, localPos, wallScale, rotation);

            go.tag = "Wall";
            var col = go.AddComponent<BoxCollider2D>();
            col.size = sprite.bounds.size;

            pos += WallWorld;
        }
    }

    // ──────────────────────────────────────────────
    //  墙体延伸 (wallTileSize×wallTileSize, 纯装饰)
    // ──────────────────────────────────────────────

    void BuildWallExtensions(Transform container, float halfW, float halfH)
    {
        BuildExtensionForDirection(container, DoorDirection.Top, halfW, halfH, true);
        BuildExtensionForDirection(container, DoorDirection.Top, halfW, halfH, false);
        BuildExtensionForDirection(container, DoorDirection.Bottom, halfW, halfH, true);
        BuildExtensionForDirection(container, DoorDirection.Bottom, halfW, halfH, false);
        BuildExtensionForDirection(container, DoorDirection.Left, halfW, halfH, true);
        BuildExtensionForDirection(container, DoorDirection.Left, halfW, halfH, false);
        BuildExtensionForDirection(container, DoorDirection.Right, halfW, halfH, true);
        BuildExtensionForDirection(container, DoorDirection.Right, halfW, halfH, false);
    }

    void BuildExtensionForDirection(Transform container, DoorDirection targetDir,
        float halfW, float halfH, bool isOut)
    {
        var sprites = isOut ? wallExtOutSprites : wallExtInSprites;
        var picker = new SpritePicker(sprites, wallPlaceMode, GetWallStartIndex(targetDir));
        if (picker.IsEmpty) return;

        bool horizontal = targetDir == DoorDirection.Top || targetDir == DoorDirection.Bottom;
        float rotation = DirToAngle(targetDir) - DirToAngle(wallExtSourceDir);

        float start, end, wallFixedPos;
        if (horizontal)
        {
            start = -halfW;
            end = halfW;
            wallFixedPos = (targetDir == DoorDirection.Top) ? halfH + HalfWall : -halfH - HalfWall;
        }
        else
        {
            start = -halfH;
            end = halfH;
            wallFixedPos = (targetDir == DoorDirection.Right) ? halfW + HalfWall : -halfW - HalfWall;
        }

        // 外侧=远离房间中心, 内侧=靠近房间中心
        float outward = (targetDir == DoorDirection.Top || targetDir == DoorDirection.Right) ? 1f : -1f;
        float extFixedPos = isOut
            ? wallFixedPos + WallWorld * outward
            : wallFixedPos - WallWorld * outward;

        Vector2 worldOffset = RotateVector2(wallExtOffset, rotation);
        string prefix = $"Wall_{targetDir}_Ext_{(isOut ? "Out" : "In")}";

        float pos = start + HalfWall;
        int i = 0;
        while (pos < end - 0.001f)
        {
            Vector3 localPos = horizontal
                ? new Vector3(pos, extFixedPos, 0)
                : new Vector3(extFixedPos, pos, 0);
            localPos += new Vector3(worldOffset.x, worldOffset.y, 0);

            Sprite sprite = picker.Next();
            CreateSprite($"{prefix}_{i++}", sprite,
                new Vector2(WallWorld, WallWorld), 0, container, localPos, wallExtScale, rotation);

            pos += WallWorld;
        }
    }

    // ──────────────────────────────────────────────
    //  墙角 (wallTileSize×wallTileSize, 多类型, 随机/顺时针有序, flipX/flipY)
    // ──────────────────────────────────────────────

    void BuildCorner(Transform container, string name, Vector3 localPos, CornerDir targetDir, SpritePicker picker)
    {
        Sprite sprite = picker.Next();
        if (sprite == null) return;

        bool flipX = IsCornerRight(targetDir) != IsCornerRight(cornerDefaultDir);
        bool flipY = IsCornerBottom(targetDir) != IsCornerBottom(cornerDefaultDir);
        float ox = flipX ? -cornerOffset.x : cornerOffset.x;
        float oy = flipY ? -cornerOffset.y : cornerOffset.y;
        localPos += new Vector3(ox, oy, 0);

        var go = CreateSprite(name, sprite,
            new Vector2(WallWorld, WallWorld), 0, container, localPos, cornerScale, 0f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.flipX = flipX;
        sr.flipY = flipY;

        go.tag = "Wall";
        var col = go.AddComponent<BoxCollider2D>();
        col.size = sprite.bounds.size;
    }

    static bool IsCornerRight(CornerDir dir) => dir is CornerDir.TopRight or CornerDir.BottomRight;
    static bool IsCornerBottom(CornerDir dir) => dir is CornerDir.BottomRight or CornerDir.BottomLeft;

    // ──────────────────────────────────────────────
    //  门
    // ──────────────────────────────────────────────

    void BuildDoor(Transform container, RoomRoot roomRoot, string name,
                   DoorDirection dir, Vector3 localPos, Vector2 offset)
    {
        var doorGO = new GameObject(name);
        doorGO.transform.SetParent(container, false);
        doorGO.transform.localPosition = localPos + new Vector3(offset.x, offset.y, 0);

        var sr = doorGO.AddComponent<SpriteRenderer>();
        sr.sprite = doorSprite;
        sr.sortingOrder = 5;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sharedMaterial = spriteMaterial;

        Vector2 nat = doorSprite.bounds.size;
        Vector2 desired = new(tileSize * doorScale.x, tileSize * doorScale.y);
        doorGO.transform.localScale = new Vector3(desired.x / nat.x, desired.y / nat.y, 1f);

        float rotZ = DirToAngle(dir) - DirToAngle(doorSpriteDefaultDir);
        doorGO.transform.localRotation = Quaternion.Euler(0, 0, rotZ);

        var trigger = doorGO.AddComponent<BoxCollider2D>();
        trigger.size = nat;
        trigger.isTrigger = true;

        var portal = doorGO.AddComponent<RoomPortal>();
        portal.direction = dir;
        portal.targetRoomId = -1;
        portal.doorSprite = sr;
        portal.openTrigger = trigger;
        portal.targetTag = "Player";

        switch (dir)
        {
            case DoorDirection.Top: roomRoot.topDoor = doorGO.transform; break;
            case DoorDirection.Bottom: roomRoot.bottomDoor = doorGO.transform; break;
            case DoorDirection.Left: roomRoot.leftDoor = doorGO.transform; break;
            case DoorDirection.Right: roomRoot.rightDoor = doorGO.transform; break;
        }
    }

    // ──────────────────────────────────────────────
    //  方向工具
    // ──────────────────────────────────────────────

    static float DirToAngle(DoorDirection dir) => dir switch
    {
        DoorDirection.Right => 0f,
        DoorDirection.Top => 90f,
        DoorDirection.Left => 180f,
        DoorDirection.Bottom => 270f,
        _ => 0f
    };

    int GetWallStartIndex(DoorDirection dir) => dir switch
    {
        DoorDirection.Top => wallStartIndexTop,
        DoorDirection.Bottom => wallStartIndexBottom,
        DoorDirection.Left => wallStartIndexLeft,
        DoorDirection.Right => wallStartIndexRight,
        _ => 0
    };

    static Vector2 RotateVector2(Vector2 v, float angleDeg)
    {
        if (angleDeg == 0f) return v;
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // ──────────────────────────────────────────────
    //  生成点
    // ──────────────────────────────────────────────

    void BuildSpawnPoints(Transform root, RoomRoot roomRoot)
    {
        float halfW = roomTilesX * tileSize * 0.5f;
        float halfH = roomTilesY * tileSize * 0.5f;
        float margin = WallWorld;   // 边距至少一个墙块厚度
        float minX = -halfW + margin, maxX = halfW - margin;
        float minY = -halfH + margin, maxY = halfH - margin;

        var enemyContainer = new GameObject("EnemySpawnPoints").transform;
        enemyContainer.SetParent(root, false);
        var eArr = new Transform[enemySpawnCount];
        for (int i = 0; i < enemySpawnCount; i++)
        {
            var go = new GameObject("E");
            go.transform.SetParent(enemyContainer, false);
            float t = enemySpawnCount > 1 ? (float)i / (enemySpawnCount - 1) : 0.5f;
            float x = Mathf.Lerp(minX, maxX, t);
            float y = (i % 2 == 0) ? Mathf.Lerp(minY, maxY, 0.33f) : Mathf.Lerp(minY, maxY, 0.67f);
            go.transform.localPosition = new Vector3(x, y, 0);
            eArr[i] = go.transform;
        }
        roomRoot.enemySpawnPoints = eArr;

        var itemContainer = new GameObject("ItemSpawnPoints").transform;
        itemContainer.SetParent(root, false);
        var iArr = new Transform[itemSpawnCount];
        for (int i = 0; i < itemSpawnCount; i++)
        {
            var go = new GameObject("I");
            go.transform.SetParent(itemContainer, false);
            float t = itemSpawnCount > 1 ? (float)i / (itemSpawnCount - 1) : 0.5f;
            float x = Mathf.Lerp(minX, maxX, 0.2f + t * 0.6f);
            float y = Mathf.Lerp(minY, maxY, 0.5f + Mathf.Sin(t * Mathf.PI) * 0.2f);
            go.transform.localPosition = new Vector3(x, y, 0);
            iArr[i] = go.transform;
        }
        roomRoot.itemSpawnPoints = iArr;
    }

    // ──────────────────────────────────────────────
    //  工具
    // ──────────────────────────────────────────────

    GameObject CreateSprite(string name, Sprite sprite, Vector2 desiredWorldSize, int sortOrder,
                           Transform parent, Vector3 localPos, Vector2 spriteScale, float rotationZ)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0, 0, rotationZ);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortOrder;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sharedMaterial = spriteMaterial;

        Vector2 nat = sprite.bounds.size;
        Vector2 target = new(desiredWorldSize.x * spriteScale.x, desiredWorldSize.y * spriteScale.y);
        go.transform.localScale = new Vector3(target.x / nat.x, target.y / nat.y, 1f);
        return go;
    }

    Sprite PickFloorSprite()
    {
        var valid = floorSprites.Where(s => s != null).ToList();
        if (valid.Count == 0) return null;
        return valid[Random.Range(0, valid.Count)];
    }

    static void Warn(string msg) => EditorUtility.DisplayDialog("提示", msg, "确定");

    static void EnsureDir(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }
}
