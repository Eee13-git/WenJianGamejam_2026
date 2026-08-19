using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 房间搭建工具 — 自动平铺地板/墙壁/门，生成房间预制体。
/// 菜单: Tools > 搭建房间
/// 支持多预案保存，配置持久化到 EditorPrefs。
/// </summary>
public class RoomBuilderTool : EditorWindow
{
    const string PresetsPrefsKey = "RoomBuilderTool_Presets";
    const string OldPrefsKey = "RoomBuilderTool_Config";

    // === 格子与房间 ===
    float tileSize = 1f;
    int roomTilesX = 20;
    int roomTilesY = 12;

    // === 地板 ===
    enum FloorMode { Tiled, Single }
    FloorMode floorMode = FloorMode.Single;
    Sprite floorSprite;
    Vector2 floorScale = new(1f, 1f), floorOffset = Vector2.zero;

    // === 墙壁 ===
    Sprite wallTopSprite, wallBottomSprite, wallLeftSprite, wallRightSprite;
    Vector2 wallTopScale = new(1f, 1f), wallTopOffset = Vector2.zero;
    Vector2 wallBottomScale = new(1f, 1f), wallBottomOffset = Vector2.zero;
    Vector2 wallLeftScale = new(1f, 1f), wallLeftOffset = Vector2.zero;
    Vector2 wallRightScale = new(1f, 1f), wallRightOffset = Vector2.zero;

    // === 墙体延伸 (可选, 纯装饰, 不阻挡, 每侧独立) ===
    Sprite wallTopExtOutSprite, wallTopExtInSprite;
    Sprite wallBottomExtOutSprite, wallBottomExtInSprite;
    Sprite wallLeftExtOutSprite, wallLeftExtInSprite;
    Sprite wallRightExtOutSprite, wallRightExtInSprite;
    Vector2 wallTopExtScale = new(1f, 1f), wallTopExtOffset = Vector2.zero;
    Vector2 wallBottomExtScale = new(1f, 1f), wallBottomExtOffset = Vector2.zero;
    Vector2 wallLeftExtScale = new(1f, 1f), wallLeftExtOffset = Vector2.zero;
    Vector2 wallRightExtScale = new(1f, 1f), wallRightExtOffset = Vector2.zero;

    // === 墙角 ===
    enum CornerDir { TopLeft, TopRight, BottomRight, BottomLeft }
    Sprite cornerSprite;
    CornerDir cornerDefaultDir = CornerDir.TopLeft;
    Vector2 cornerTLScale = new(1f, 1f), cornerTRScale = new(1f, 1f);
    Vector2 cornerBRScale = new(1f, 1f), cornerBLScale = new(1f, 1f);
    Vector2 cornerTLOffset = Vector2.zero, cornerTROffset = Vector2.zero;
    Vector2 cornerBROffset = Vector2.zero, cornerBLOffset = Vector2.zero;

    // === 门 ===
    Sprite doorSprite;
    DoorDirection doorSpriteDefaultDir = DoorDirection.Right;
    Vector2 doorScale = new(1f, 1f);
    Vector2 doorTopOffset = Vector2.zero, doorBottomOffset = Vector2.zero;
    Vector2 doorLeftOffset = Vector2.zero, doorRightOffset = Vector2.zero;

    // === RoomRoot 数据 ===
    int enemySpawnCount = 8;
    int itemSpawnCount = 5;

    // === 网格编辑 ===
    enum GridEditMode { None, EnemySpawn, ItemSpawn }
    GridEditMode _gridEditMode = GridEditMode.None;
    HashSet<Vector2Int> _enemyCells = new();
    HashSet<Vector2Int> _itemCells = new();
    Vector2 _gridScroll;
    float _gridCellSize = 24f;
    Vector2Int? _hoverCell;

    // === 材质 ===
    Material spriteMaterial;

    // === 输出 ===
    string prefabName = "Room_Normal_1F";
    string outputFolder = "Assets/Prefabs/Rooms";

    // === 预案系统 ===
    [System.Serializable]
    class ConfigData
    {
        public float tileSize = 1f;
        public int roomTilesX = 20, roomTilesY = 12;
        public int floorMode = 1;
        public string floorSpriteGUID;
        public Vector2 floorScale = new(1f, 1f), floorOffset = Vector2.zero;
        public string wallTopGUID, wallBottomGUID, wallLeftGUID, wallRightGUID;
        public Vector2 wallTopScale = new(1f, 1f), wallTopOffset = Vector2.zero;
        public Vector2 wallBottomScale = new(1f, 1f), wallBottomOffset = Vector2.zero;
        public Vector2 wallLeftScale = new(1f, 1f), wallLeftOffset = Vector2.zero;
        public Vector2 wallRightScale = new(1f, 1f), wallRightOffset = Vector2.zero;
        public string wallTopExtOutGUID, wallTopExtInGUID, wallBottomExtOutGUID, wallBottomExtInGUID;
        public string wallLeftExtOutGUID, wallLeftExtInGUID, wallRightExtOutGUID, wallRightExtInGUID;
        public Vector2 wallTopExtScale = new(1f, 1f), wallTopExtOffset = Vector2.zero;
        public Vector2 wallBottomExtScale = new(1f, 1f), wallBottomExtOffset = Vector2.zero;
        public Vector2 wallLeftExtScale = new(1f, 1f), wallLeftExtOffset = Vector2.zero;
        public Vector2 wallRightExtScale = new(1f, 1f), wallRightExtOffset = Vector2.zero;
        public string cornerSpriteGUID;
        public int cornerDefaultDir;
        public Vector2 cornerTLScale = new(1f, 1f), cornerTRScale = new(1f, 1f);
        public Vector2 cornerBRScale = new(1f, 1f), cornerBLScale = new(1f, 1f);
        public Vector2 cornerTLOffset = Vector2.zero, cornerTROffset = Vector2.zero;
        public Vector2 cornerBROffset = Vector2.zero, cornerBLOffset = Vector2.zero;
        public string doorSpriteGUID;
        public int doorSpriteDefaultDir;
        public Vector2 doorScale = new(1f, 1f);
        public Vector2 doorTopOffset = Vector2.zero, doorBottomOffset = Vector2.zero;
        public Vector2 doorLeftOffset = Vector2.zero, doorRightOffset = Vector2.zero;
        public int enemySpawnCount = 8, itemSpawnCount = 5;
        public List<int> enemyCellX = new(), enemyCellY = new();
        public List<int> itemCellX = new(), itemCellY = new();
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

    [MenuItem("Tools/搭建房间")]
    static void ShowWindow() => GetWindow<RoomBuilderTool>("房间搭建工具");

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
        {
            _presets = JsonUtility.FromJson<PresetList>(json);
        }

        // 旧数据迁移
        if (_presets == null || _presets.presets.Count == 0)
        {
            var oldJson = EditorPrefs.GetString(OldPrefsKey, "");
            _presets = new PresetList();
            if (!string.IsNullOrEmpty(oldJson))
            {
                var d = JsonUtility.FromJson<ConfigData>(oldJson);
                _presets.presets.Add(new PresetData { name = "默认", config = d });
                _presets.currentName = "默认";
            }
            else
            {
                _presets.presets.Add(new PresetData { name = "默认", config = new ConfigData() });
                _presets.currentName = "默认";
            }
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
        roomTilesX = d.roomTilesX;
        roomTilesY = d.roomTilesY;
        floorMode = (FloorMode)d.floorMode;
        floorSprite = FromGUID<Sprite>(d.floorSpriteGUID);
        floorScale = d.floorScale;
        floorOffset = d.floorOffset;
        wallTopSprite = FromGUID<Sprite>(d.wallTopGUID);
        wallBottomSprite = FromGUID<Sprite>(d.wallBottomGUID);
        wallLeftSprite = FromGUID<Sprite>(d.wallLeftGUID);
        wallRightSprite = FromGUID<Sprite>(d.wallRightGUID);
        wallTopScale = d.wallTopScale; wallTopOffset = d.wallTopOffset;
        wallBottomScale = d.wallBottomScale; wallBottomOffset = d.wallBottomOffset;
        wallLeftScale = d.wallLeftScale; wallLeftOffset = d.wallLeftOffset;
        wallRightScale = d.wallRightScale; wallRightOffset = d.wallRightOffset;
        wallTopExtOutSprite = FromGUID<Sprite>(d.wallTopExtOutGUID);
        wallTopExtInSprite = FromGUID<Sprite>(d.wallTopExtInGUID);
        wallBottomExtOutSprite = FromGUID<Sprite>(d.wallBottomExtOutGUID);
        wallBottomExtInSprite = FromGUID<Sprite>(d.wallBottomExtInGUID);
        wallLeftExtOutSprite = FromGUID<Sprite>(d.wallLeftExtOutGUID);
        wallLeftExtInSprite = FromGUID<Sprite>(d.wallLeftExtInGUID);
        wallRightExtOutSprite = FromGUID<Sprite>(d.wallRightExtOutGUID);
        wallRightExtInSprite = FromGUID<Sprite>(d.wallRightExtInGUID);
        wallTopExtScale = d.wallTopExtScale; wallTopExtOffset = d.wallTopExtOffset;
        wallBottomExtScale = d.wallBottomExtScale; wallBottomExtOffset = d.wallBottomExtOffset;
        wallLeftExtScale = d.wallLeftExtScale; wallLeftExtOffset = d.wallLeftExtOffset;
        wallRightExtScale = d.wallRightExtScale; wallRightExtOffset = d.wallRightExtOffset;
        cornerSprite = FromGUID<Sprite>(d.cornerSpriteGUID);
        cornerDefaultDir = (CornerDir)d.cornerDefaultDir;
        cornerTLScale = d.cornerTLScale; cornerTLOffset = d.cornerTLOffset;
        cornerTRScale = d.cornerTRScale; cornerTROffset = d.cornerTROffset;
        cornerBRScale = d.cornerBRScale; cornerBROffset = d.cornerBROffset;
        cornerBLScale = d.cornerBLScale; cornerBLOffset = d.cornerBLOffset;
        doorSprite = FromGUID<Sprite>(d.doorSpriteGUID);
        doorSpriteDefaultDir = (DoorDirection)d.doorSpriteDefaultDir;
        doorScale = d.doorScale;
        doorTopOffset = d.doorTopOffset; doorBottomOffset = d.doorBottomOffset;
        doorLeftOffset = d.doorLeftOffset; doorRightOffset = d.doorRightOffset;
        enemySpawnCount = d.enemySpawnCount;
        itemSpawnCount = d.itemSpawnCount;
        _enemyCells.Clear();
        if (d.enemyCellX != null && d.enemyCellY != null)
            for (int i = 0; i < d.enemyCellX.Count && i < d.enemyCellY.Count; i++)
                _enemyCells.Add(new Vector2Int(d.enemyCellX[i], d.enemyCellY[i]));
        _itemCells.Clear();
        if (d.itemCellX != null && d.itemCellY != null)
            for (int i = 0; i < d.itemCellX.Count && i < d.itemCellY.Count; i++)
                _itemCells.Add(new Vector2Int(d.itemCellX[i], d.itemCellY[i]));
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
            roomTilesX = roomTilesX,
            roomTilesY = roomTilesY,
            floorMode = (int)floorMode,
            floorSpriteGUID = ToGUID(floorSprite),
            floorScale = floorScale,
            floorOffset = floorOffset,
            wallTopGUID = ToGUID(wallTopSprite),
            wallBottomGUID = ToGUID(wallBottomSprite),
            wallLeftGUID = ToGUID(wallLeftSprite),
            wallRightGUID = ToGUID(wallRightSprite),
            wallTopScale = wallTopScale, wallTopOffset = wallTopOffset,
            wallBottomScale = wallBottomScale, wallBottomOffset = wallBottomOffset,
            wallLeftScale = wallLeftScale, wallLeftOffset = wallLeftOffset,
            wallRightScale = wallRightScale, wallRightOffset = wallRightOffset,
            wallTopExtOutGUID = ToGUID(wallTopExtOutSprite),
            wallTopExtInGUID = ToGUID(wallTopExtInSprite),
            wallBottomExtOutGUID = ToGUID(wallBottomExtOutSprite),
            wallBottomExtInGUID = ToGUID(wallBottomExtInSprite),
            wallLeftExtOutGUID = ToGUID(wallLeftExtOutSprite),
            wallLeftExtInGUID = ToGUID(wallLeftExtInSprite),
            wallRightExtOutGUID = ToGUID(wallRightExtOutSprite),
            wallRightExtInGUID = ToGUID(wallRightExtInSprite),
            wallTopExtScale = wallTopExtScale, wallTopExtOffset = wallTopExtOffset,
            wallBottomExtScale = wallBottomExtScale, wallBottomExtOffset = wallBottomExtOffset,
            wallLeftExtScale = wallLeftExtScale, wallLeftExtOffset = wallLeftExtOffset,
            wallRightExtScale = wallRightExtScale, wallRightExtOffset = wallRightExtOffset,
            cornerSpriteGUID = ToGUID(cornerSprite),
            cornerDefaultDir = (int)cornerDefaultDir,
            cornerTLScale = cornerTLScale, cornerTLOffset = cornerTLOffset,
            cornerTRScale = cornerTRScale, cornerTROffset = cornerTROffset,
            cornerBRScale = cornerBRScale, cornerBROffset = cornerBROffset,
            cornerBLScale = cornerBLScale, cornerBLOffset = cornerBLOffset,
            doorSpriteGUID = ToGUID(doorSprite),
            doorSpriteDefaultDir = (int)doorSpriteDefaultDir,
            doorScale = doorScale,
            doorTopOffset = doorTopOffset, doorBottomOffset = doorBottomOffset,
            doorLeftOffset = doorLeftOffset, doorRightOffset = doorRightOffset,
            enemySpawnCount = enemySpawnCount,
            itemSpawnCount = itemSpawnCount,
            enemyCellX = _enemyCells.Select(c => c.x).ToList(),
            enemyCellY = _enemyCells.Select(c => c.y).ToList(),
            itemCellX = _itemCells.Select(c => c.x).ToList(),
            itemCellY = _itemCells.Select(c => c.y).ToList(),
            materialGUID = ToGUID(spriteMaterial),
            prefabName = prefabName,
            outputFolder = outputFolder
        };
    }

    // ──────────────────────────────────────────────
    //  UI
    // ──────────────────────────────────────────────

    Vector2 _scroll;

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("房间搭建工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("自动平铺地板/墙壁/门，生成房间预制体。\n配置自动保存，关闭 Unity 不丢失。\n血栓（障碍物）需手动放置。", MessageType.Info);

        // ── 预案管理 ──
        EditorGUILayout.Space();
        GUILayout.Label("预案", EditorStyles.miniBoldLabel);

        // 内联重命名
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
            {
                _renaming = false;
            }
            EditorGUILayout.EndHorizontal();
        }

        // 预案下拉 + 按钮
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
            {
                Warn("至少保留一个预案");
            }
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

        // 新建预案
        EditorGUILayout.BeginHorizontal();
        _newPresetName = EditorGUILayout.TextField("新预案名称", _newPresetName);
        if (GUILayout.Button("新建", GUILayout.Width(50)))
        {
            if (string.IsNullOrWhiteSpace(_newPresetName))
            {
                Warn("请输入预案名称");
            }
            else if (_presets.presets.Any(p => p.name == _newPresetName))
            {
                Warn("预案名称已存在");
            }
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

        // ── 格子与房间 ──
        EditorGUILayout.Space();
        GUILayout.Label("格子 & 房间", EditorStyles.miniBoldLabel);
        tileSize = EditorGUILayout.FloatField("格子大小 (世界单位)", tileSize);
        roomTilesX = EditorGUILayout.IntField("房间宽 (格子数)", roomTilesX);
        roomTilesY = EditorGUILayout.IntField("房间高 (格子数)", roomTilesY);

        // ── 地板 ──
        EditorGUILayout.Space();
        GUILayout.Label("地板", EditorStyles.miniBoldLabel);
        floorMode = (FloorMode)EditorGUILayout.EnumPopup("地板模式", floorMode);
        floorSprite = (Sprite)EditorGUILayout.ObjectField("地板素材", floorSprite, typeof(Sprite), false);
        floorScale = EditorGUILayout.Vector2Field("地板缩放 (X=横向, Y=纵向)", floorScale);
        floorOffset = EditorGUILayout.Vector2Field("地板偏移 (X=横向, Y=纵向)", floorOffset);

        // ── 墙壁 ──
        EditorGUILayout.Space();
        GUILayout.Label("墙壁 (各方向独立配置)", EditorStyles.miniBoldLabel);
        wallTopSprite = (Sprite)EditorGUILayout.ObjectField("上墙", wallTopSprite, typeof(Sprite), false);
        wallTopScale = EditorGUILayout.Vector2Field("上墙缩放 (X=横向, Y=纵向)", wallTopScale);
        wallTopOffset = EditorGUILayout.Vector2Field("上墙偏移 (X=横向, Y=纵向)", wallTopOffset);
        wallBottomSprite = (Sprite)EditorGUILayout.ObjectField("下墙", wallBottomSprite, typeof(Sprite), false);
        wallBottomScale = EditorGUILayout.Vector2Field("下墙缩放 (X=横向, Y=纵向)", wallBottomScale);
        wallBottomOffset = EditorGUILayout.Vector2Field("下墙偏移 (X=横向, Y=纵向)", wallBottomOffset);
        wallLeftSprite = (Sprite)EditorGUILayout.ObjectField("左墙", wallLeftSprite, typeof(Sprite), false);
        wallLeftScale = EditorGUILayout.Vector2Field("左墙缩放 (X=横向, Y=纵向)", wallLeftScale);
        wallLeftOffset = EditorGUILayout.Vector2Field("左墙偏移 (X=横向, Y=纵向)", wallLeftOffset);
        wallRightSprite = (Sprite)EditorGUILayout.ObjectField("右墙", wallRightSprite, typeof(Sprite), false);
        wallRightScale = EditorGUILayout.Vector2Field("右墙缩放 (X=横向, Y=纵向)", wallRightScale);
        wallRightOffset = EditorGUILayout.Vector2Field("右墙偏移 (X=横向, Y=纵向)", wallRightOffset);

        // ── 墙体延伸 (可选, 纯装饰不阻挡, 每侧独立) ──
        EditorGUILayout.Space();
        GUILayout.Label("墙体延伸 (可选, 纯装饰不阻挡)", EditorStyles.miniBoldLabel);

        wallTopExtOutSprite = (Sprite)EditorGUILayout.ObjectField("上墙外侧", wallTopExtOutSprite, typeof(Sprite), false);
        wallTopExtInSprite = (Sprite)EditorGUILayout.ObjectField("上墙内侧", wallTopExtInSprite, typeof(Sprite), false);
        wallTopExtScale = EditorGUILayout.Vector2Field("上墙延伸缩放 (X=横向, Y=纵向)", wallTopExtScale);
        wallTopExtOffset = EditorGUILayout.Vector2Field("上墙延伸偏移 (X=横向, Y=纵向)", wallTopExtOffset);

        wallBottomExtOutSprite = (Sprite)EditorGUILayout.ObjectField("下墙外侧", wallBottomExtOutSprite, typeof(Sprite), false);
        wallBottomExtInSprite = (Sprite)EditorGUILayout.ObjectField("下墙内侧", wallBottomExtInSprite, typeof(Sprite), false);
        wallBottomExtScale = EditorGUILayout.Vector2Field("下墙延伸缩放 (X=横向, Y=纵向)", wallBottomExtScale);
        wallBottomExtOffset = EditorGUILayout.Vector2Field("下墙延伸偏移 (X=横向, Y=纵向)", wallBottomExtOffset);

        wallLeftExtOutSprite = (Sprite)EditorGUILayout.ObjectField("左墙外侧", wallLeftExtOutSprite, typeof(Sprite), false);
        wallLeftExtInSprite = (Sprite)EditorGUILayout.ObjectField("左墙内侧", wallLeftExtInSprite, typeof(Sprite), false);
        wallLeftExtScale = EditorGUILayout.Vector2Field("左墙延伸缩放 (X=横向, Y=纵向)", wallLeftExtScale);
        wallLeftExtOffset = EditorGUILayout.Vector2Field("左墙延伸偏移 (X=横向, Y=纵向)", wallLeftExtOffset);

        wallRightExtOutSprite = (Sprite)EditorGUILayout.ObjectField("右墙外侧", wallRightExtOutSprite, typeof(Sprite), false);
        wallRightExtInSprite = (Sprite)EditorGUILayout.ObjectField("右墙内侧", wallRightExtInSprite, typeof(Sprite), false);
        wallRightExtScale = EditorGUILayout.Vector2Field("右墙延伸缩放 (X=横向, Y=纵向)", wallRightExtScale);
        wallRightExtOffset = EditorGUILayout.Vector2Field("右墙延伸偏移 (X=横向, Y=纵向)", wallRightExtOffset);

        // ── 墙角 ──
        EditorGUILayout.Space();
        GUILayout.Label("墙角 (单素材, 可配置默认朝向)", EditorStyles.miniBoldLabel);
        cornerSprite = (Sprite)EditorGUILayout.ObjectField("墙角素材", cornerSprite, typeof(Sprite), false);
        cornerDefaultDir = (CornerDir)EditorGUILayout.EnumPopup("素材默认朝向", cornerDefaultDir);
        cornerTLScale = EditorGUILayout.Vector2Field("左上角缩放 (X=横向, Y=纵向)", cornerTLScale);
        cornerTLOffset = EditorGUILayout.Vector2Field("左上角偏移 (X=横向, Y=纵向)", cornerTLOffset);
        cornerTRScale = EditorGUILayout.Vector2Field("右上角缩放 (X=横向, Y=纵向)", cornerTRScale);
        cornerTROffset = EditorGUILayout.Vector2Field("右上角偏移 (X=横向, Y=纵向)", cornerTROffset);
        cornerBRScale = EditorGUILayout.Vector2Field("右下角缩放 (X=横向, Y=纵向)", cornerBRScale);
        cornerBROffset = EditorGUILayout.Vector2Field("右下角偏移 (X=横向, Y=纵向)", cornerBROffset);
        cornerBLScale = EditorGUILayout.Vector2Field("左下角缩放 (X=横向, Y=纵向)", cornerBLScale);
        cornerBLOffset = EditorGUILayout.Vector2Field("左下角偏移 (X=横向, Y=纵向)", cornerBLOffset);

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

        // ── 材质 & RoomRoot 数据 ──
        EditorGUILayout.Space();
        GUILayout.Label("材质 & RoomRoot", EditorStyles.miniBoldLabel);
        spriteMaterial = (Material)EditorGUILayout.ObjectField("材质", spriteMaterial, typeof(Material), false);
        enemySpawnCount = EditorGUILayout.IntSlider("敌人生成点数量", enemySpawnCount, 0, 20);
        itemSpawnCount = EditorGUILayout.IntSlider("道具生成点数量", itemSpawnCount, 0, 20);
        EditorGUILayout.LabelField("房间边界 (roomSize)", RoomSize.ToString("F1"));

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

        // ── 生成点网格编辑 ──
        GUILayout.Label("生成点编辑", EditorStyles.miniBoldLabel);
        _gridEditMode = (GridEditMode)EditorGUILayout.EnumPopup("编辑模式", _gridEditMode);
        _gridCellSize = EditorGUILayout.Slider("格子像素大小", _gridCellSize, 12f, 40f);

        EditorGUILayout.BeginHorizontal();
        DrawLegendGrid(new Color(0.6f, 0.6f, 0.6f, 0.3f), "空");
        DrawLegendGrid(new Color(0.2f, 0.5f, 0.9f, 0.7f), "敌人点");
        DrawLegendGrid(new Color(0.7f, 0.3f, 0.9f, 0.7f), "道具点");
        EditorGUILayout.EndHorizontal();

        DrawSpawnGrid();

        if (_gridEditMode != GridEditMode.None)
            EditorGUILayout.HelpBox("点击格子放置/删除生成点，敌人点和道具点互斥", MessageType.None);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("生成房间预制体", GUILayout.Height(30)))
        {
            SaveCurrentToPresets();
            SavePresetsToDisk();
            BuildRoom();
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            SaveCurrentToPresets();
            SavePresetsToDisk();
        }
    }

    // ──────────────────────────────────────────────
    //  生成点网格
    // ──────────────────────────────────────────────

    void DrawLegendGrid(Color color, string label)
    {
        var rect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
        EditorGUI.DrawRect(rect, color);
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(50));
    }

    void DrawSpawnGrid()
    {
        int halfX = roomTilesX / 2;
        int halfY = roomTilesY / 2;
        float gridW = roomTilesX * _gridCellSize;
        float gridH = roomTilesY * _gridCellSize;

        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll,
            GUILayout.Height(Mathf.Min(gridH + 40, 400)),
            GUILayout.MaxWidth(gridW + 30));

        EditorGUILayout.BeginVertical(GUILayout.Width(gridW));

        for (int row = roomTilesY - 1; row >= 0; row--)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(gridW));
            for (int col = 0; col < roomTilesX; col++)
            {
                var cell = new Vector2Int(col - halfX, row - halfY);

                Color bg;
                string label = "";

                if (_enemyCells.Contains(cell))
                {
                    bg = new Color(0.2f, 0.5f, 0.9f, 0.7f);
                    label = "E";
                }
                else if (_itemCells.Contains(cell))
                {
                    bg = new Color(0.7f, 0.3f, 0.9f, 0.7f);
                    label = "I";
                }
                else
                    bg = new Color(0.6f, 0.6f, 0.6f, 0.3f);

                bool isHover = _hoverCell.HasValue && _hoverCell.Value == cell;
                if (isHover)
                    bg = Color.Lerp(bg, Color.yellow, 0.4f);

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = bg;

                if (GUILayout.Button(label, GUILayout.Width(_gridCellSize), GUILayout.Height(_gridCellSize)))
                {
                    OnGridCellClicked(cell);
                    Repaint();
                }

                if (Event.current.type == EventType.Repaint)
                {
                    var btnRect = GUILayoutUtility.GetLastRect();
                    if (btnRect.Contains(Event.current.mousePosition))
                        _hoverCell = cell;
                }

                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    void OnGridCellClicked(Vector2Int cell)
    {
        switch (_gridEditMode)
        {
            case GridEditMode.EnemySpawn:
                if (_itemCells.Contains(cell)) return;
                if (_enemyCells.Contains(cell))
                    _enemyCells.Remove(cell);
                else
                    _enemyCells.Add(cell);
                break;
            case GridEditMode.ItemSpawn:
                if (_enemyCells.Contains(cell)) return;
                if (_itemCells.Contains(cell))
                    _itemCells.Remove(cell);
                else
                    _itemCells.Add(cell);
                break;
        }
    }

    Vector3 SpawnCellToWorld(Vector2Int cell)
    {
        float ox = (roomTilesX % 2 == 0) ? 0.5f * tileSize : 0f;
        float oy = (roomTilesY % 2 == 0) ? 0.5f * tileSize : 0f;
        return new Vector3(cell.x * tileSize + ox, cell.y * tileSize + oy, 0);
    }

    // ──────────────────────────────────────────────
    //  构建
    // ──────────────────────────────────────────────

    Vector2 RoomSize => new(roomTilesX * tileSize, roomTilesY * tileSize);

    void BuildRoom()
    {
        if (spriteMaterial == null) { Warn("请指定材质"); return; }
        if (floorSprite == null) { Warn("请指定地板素材"); return; }
        if (doorSprite == null) { Warn("请指定门素材"); return; }
        if (!wallTopSprite && !wallBottomSprite && !wallLeftSprite && !wallRightSprite) { Warn("至少指定一面墙的素材"); return; }

        string prefabPath = $"{outputFolder}/{prefabName}.prefab";
        EnsureDir(prefabPath);

        var root = new GameObject(prefabName);
        var roomRoot = root.AddComponent<RoomRoot>();
        roomRoot.roomSize = RoomSize;

        if (floorMode == FloorMode.Single)
        {
            // 地板多铺一圈覆盖墙体所在格子
            Vector2 floorWorldSize = new(RoomSize.x + tileSize * 2f, RoomSize.y + tileSize * 2f);
            CreateSprite("Floor", floorSprite, floorWorldSize, -2, root.transform, new Vector3(floorOffset.x, floorOffset.y, 0), floorScale);
        }
        else
            BuildTiledFloor(root.transform);

        var wallsGO = new GameObject("Walls");
        wallsGO.transform.SetParent(root.transform, false);
        BuildWalls(wallsGO.transform);

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
        Debug.Log($"[RoomBuilder] 已生成: {prefabPath}");
        EditorUtility.DisplayDialog("完成", $"房间预制体已生成:\n{prefabPath}", "确定");
    }

    // ──────────────────────────────────────────────
    //  地板
    // ──────────────────────────────────────────────

    void BuildTiledFloor(Transform parent)
    {
        var container = new GameObject("Floor").transform;
        container.SetParent(parent, false);
        container.localPosition = Vector3.zero;

        float halfW = roomTilesX * tileSize * 0.5f;
        float halfH = roomTilesY * tileSize * 0.5f;

        // 多铺一圈覆盖墙体所在格子
        for (int y = -1; y <= roomTilesY; y++)
        {
            for (int x = -1; x <= roomTilesX; x++)
            {
                float px = -halfW + (x + 0.5f) * tileSize;
                float py = -halfH + (y + 0.5f) * tileSize;
                CreateSprite($"Floor_{x}_{y}", floorSprite,
                    new Vector2(tileSize, tileSize), -2, container,
                    new Vector3(px + floorOffset.x, py + floorOffset.y, 0), floorScale);
            }
        }
    }

    // ──────────────────────────────────────────────
    //  墙壁
    // ──────────────────────────────────────────────

    void BuildWalls(Transform container)
    {
        float halfW = roomTilesX * tileSize * 0.5f;
        float halfH = roomTilesY * tileSize * 0.5f;
        float halfTile = tileSize * 0.5f;

        BuildWallLine(container, wallTopSprite, "Wall_Top", true, -halfW, halfW, halfH + halfTile, wallTopScale, wallTopOffset);
        BuildWallLine(container, wallBottomSprite, "Wall_Bottom", true, -halfW, halfW, -halfH - halfTile, wallBottomScale, wallBottomOffset);
        BuildWallLine(container, wallLeftSprite, "Wall_Left", false, -halfH, halfH, -halfW - halfTile, wallLeftScale, wallLeftOffset);
        BuildWallLine(container, wallRightSprite, "Wall_Right", false, -halfH, halfH, halfW + halfTile, wallRightScale, wallRightOffset);

        BuildCorner(container, cornerSprite, "Corner_TL", new Vector3(-halfW - halfTile, halfH + halfTile, 0), CornerDir.TopLeft, cornerTLOffset, cornerTLScale);
        BuildCorner(container, cornerSprite, "Corner_TR", new Vector3(halfW + halfTile, halfH + halfTile, 0), CornerDir.TopRight, cornerTROffset, cornerTRScale);
        BuildCorner(container, cornerSprite, "Corner_BR", new Vector3(halfW + halfTile, -halfH - halfTile, 0), CornerDir.BottomRight, cornerBROffset, cornerBRScale);
        BuildCorner(container, cornerSprite, "Corner_BL", new Vector3(-halfW - halfTile, -halfH - halfTile, 0), CornerDir.BottomLeft, cornerBLOffset, cornerBLScale);

        BuildWallExtensions(container);
    }

    /// <summary>
    /// 墙体延伸：每面墙两侧各一排纯装饰贴图（无碰撞器, 不阻挡）
    /// </summary>
    void BuildWallExtensions(Transform container)
    {
        float halfW = roomTilesX * tileSize * 0.5f;
        float halfH = roomTilesY * tileSize * 0.5f;
        float halfTile = tileSize * 0.5f;
        float wallOffset = halfTile + tileSize;

        // 上墙延伸：外侧(上方, 离墙一格) + 内侧(下方, 紧挨墙)
        BuildExtensionLine(container, wallTopExtOutSprite, "Wall_TopExt_Out", true, -halfW, halfW, halfH + wallOffset, wallTopExtScale, wallTopExtOffset);
        BuildExtensionLine(container, wallTopExtInSprite, "Wall_TopExt_In", true, -halfW, halfW, halfH - halfTile, wallTopExtScale, wallTopExtOffset);

        // 下墙延伸：外侧(下方, 离墙一格) + 内侧(上方, 紧挨墙)
        BuildExtensionLine(container, wallBottomExtOutSprite, "Wall_BotExt_Out", true, -halfW, halfW, -(halfH + wallOffset), wallBottomExtScale, wallBottomExtOffset);
        BuildExtensionLine(container, wallBottomExtInSprite, "Wall_BotExt_In", true, -halfW, halfW, -(halfH - halfTile), wallBottomExtScale, wallBottomExtOffset);

        // 左墙延伸：外侧(左方, 离墙一格) + 内侧(右方, 紧挨墙)
        BuildExtensionLine(container, wallLeftExtOutSprite, "Wall_LeftExt_Out", false, -halfH, halfH, -(halfW + wallOffset), wallLeftExtScale, wallLeftExtOffset);
        BuildExtensionLine(container, wallLeftExtInSprite, "Wall_LeftExt_In", false, -halfH, halfH, -(halfW - halfTile), wallLeftExtScale, wallLeftExtOffset);

        // 右墙延伸：外侧(右方, 离墙一格) + 内侧(左方, 紧挨墙)
        BuildExtensionLine(container, wallRightExtOutSprite, "Wall_RightExt_Out", false, -halfH, halfH, halfW + wallOffset, wallRightExtScale, wallRightExtOffset);
        BuildExtensionLine(container, wallRightExtInSprite, "Wall_RightExt_In", false, -halfH, halfH, halfW - halfTile, wallRightExtScale, wallRightExtOffset);
    }

    /// <summary>
    /// 纯装饰延伸行：只创建 SpriteRenderer, 无碰撞器, 无 Wall tag
    /// </summary>
    void BuildExtensionLine(Transform container, Sprite sprite, string prefix,
                            bool horizontal, float start, float end, float fixedPos, Vector2 scale, Vector2 offset)
    {
        if (sprite == null) return;
        float length = end - start;
        if (length <= 0.001f) return;

        float pos = start + tileSize * 0.5f;
        int i = 0;
        while (pos < end)
        {
            Vector3 localPos = horizontal
                ? new Vector3(pos, fixedPos, 0)
                : new Vector3(fixedPos, pos, 0);
            localPos += new Vector3(offset.x, offset.y, 0);

            CreateSprite($"{prefix}_{i++}", sprite,
                new Vector2(tileSize, tileSize), 0, container, localPos, scale);
            pos += tileSize;
        }
    }

    void BuildCorner(Transform container, Sprite sprite, string name, Vector3 localPos, CornerDir targetDir, Vector2 offset, Vector2 scale)
    {
        if (sprite == null) return;
        localPos += new Vector3(offset.x, offset.y, 0);
        var go = CreateSprite(name, sprite, new Vector2(tileSize, tileSize), 0, container, localPos, scale);

        bool targetRight = targetDir is CornerDir.TopRight or CornerDir.BottomRight;
        bool defaultRight = cornerDefaultDir is CornerDir.TopRight or CornerDir.BottomRight;
        bool targetBottom = targetDir is CornerDir.BottomRight or CornerDir.BottomLeft;
        bool defaultBottom = cornerDefaultDir is CornerDir.BottomRight or CornerDir.BottomLeft;

        var sr = go.GetComponent<SpriteRenderer>();
        sr.flipX = targetRight != defaultRight;
        sr.flipY = targetBottom != defaultBottom;

        go.tag = "Wall";
        var col = go.AddComponent<BoxCollider2D>();
        col.size = sprite.bounds.size;
    }

    void BuildWallLine(Transform container, Sprite sprite, string prefix,
                       bool horizontal, float start, float end, float fixedPos, Vector2 scale, Vector2 offset)
    {
        if (sprite == null) return;
        float length = end - start;
        if (length <= 0.001f) return;

        float pos = start + tileSize * 0.5f;
        int i = 0;
        while (pos < end)
        {
            Vector3 localPos = horizontal
                ? new Vector3(pos, fixedPos, 0)
                : new Vector3(fixedPos, pos, 0);
            localPos += new Vector3(offset.x, offset.y, 0);

            var go = CreateSprite($"{prefix}_{i++}", sprite,
                new Vector2(tileSize, tileSize), 0, container, localPos, scale);
            go.tag = "Wall";
            var col = go.AddComponent<BoxCollider2D>();
            col.size = sprite.bounds.size;
            pos += tileSize;
        }
    }

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

    static float DirToAngle(DoorDirection dir) => dir switch
    {
        DoorDirection.Right => 0f,
        DoorDirection.Top => 90f,
        DoorDirection.Left => 180f,
        DoorDirection.Bottom => 270f,
        _ => 0f
    };

    // ──────────────────────────────────────────────
    //  生成点
    // ──────────────────────────────────────────────

    void BuildSpawnPoints(Transform root, RoomRoot roomRoot)
    {
        float halfW = roomTilesX * tileSize * 0.5f;
        float halfH = roomTilesY * tileSize * 0.5f;
        float margin = tileSize * 2f;
        float minX = -halfW + margin, maxX = halfW - margin;
        float minY = -halfH + margin, maxY = halfH - margin;

        // 敌人生成点
        var enemyContainer = new GameObject("EnemySpawnPoints").transform;
        enemyContainer.SetParent(root, false);

        Transform[] eArr;
        if (_enemyCells.Count > 0)
        {
            // 使用网格手动放置的格子
            eArr = new Transform[_enemyCells.Count];
            int ei = 0;
            foreach (var cell in _enemyCells)
            {
                var go = new GameObject("E");
                go.transform.SetParent(enemyContainer, false);
                go.transform.localPosition = SpawnCellToWorld(cell);
                eArr[ei++] = go.transform;
            }
        }
        else
        {
            // 自动布局
            eArr = new Transform[enemySpawnCount];
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
        }
        roomRoot.enemySpawnPoints = eArr;

        // 道具生成点
        var itemContainer = new GameObject("ItemSpawnPoints").transform;
        itemContainer.SetParent(root, false);

        Transform[] iArr;
        if (_itemCells.Count > 0)
        {
            // 使用网格手动放置的格子
            iArr = new Transform[_itemCells.Count];
            int ii = 0;
            foreach (var cell in _itemCells)
            {
                var go = new GameObject("I");
                go.transform.SetParent(itemContainer, false);
                go.transform.localPosition = SpawnCellToWorld(cell);
                iArr[ii++] = go.transform;
            }
        }
        else
        {
            // 自动布局
            iArr = new Transform[itemSpawnCount];
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
        }
        roomRoot.itemSpawnPoints = iArr;
    }

    // ──────────────────────────────────────────────
    //  工具
    // ──────────────────────────────────────────────

    GameObject CreateSprite(string name, Sprite sprite, Vector2 desiredWorldSize, int sortOrder,
                           Transform parent, Vector3 localPos, Vector2 spriteScale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

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

    static void Warn(string msg) => EditorUtility.DisplayDialog("提示", msg, "确定");

    static void EnsureDir(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }
}
