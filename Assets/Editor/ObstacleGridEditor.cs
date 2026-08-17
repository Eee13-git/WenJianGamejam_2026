using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 障碍物网格编辑器 — 2D 俯视网格，点击格子放置/删除障碍物、敌人生成点、道具生成点。
/// 菜单: Tools > 障碍物网格编辑器
/// 保证位于格子正中心。每种障碍物独立记录预制体类型，切换类型不会覆盖已有障碍物。
/// 支持预案保存，配置持久化到 EditorPrefs。
/// </summary>
public class ObstacleGridEditor : EditorWindow
{
    const string PresetsPrefsKey = "ObstacleGridEditor_Presets";

    // === 编辑模式 ===
    enum EditMode { Obstacle, EnemySpawn, ItemSpawn }
    EditMode editMode = EditMode.Obstacle;

    // === 配置 ===
    GameObject roomPrefab;
    List<GameObject> obstaclePrefabs = new();
    int selectedObstacleIndex = 0;
    float cellSize = 24f;

    // === 运行时数据 ===
    Vector2 roomSize = new(20, 12);
    float cellOffsetX = 0.5f;
    float cellOffsetY = 0.5f;
    Dictionary<Vector2Int, int> obstacleCellMap = new();
    HashSet<Vector2Int> enemySpawnCells = new();
    HashSet<Vector2Int> itemSpawnCells = new();
    bool needsRefresh = true;
    Vector2Int? hoverCell;
    Vector2 scrollPos;

    // 障碍物类型颜色（循环使用，区分不同预制体）
    static readonly Color[] ObstacleColors =
    {
        new(0.2f, 0.8f, 0.2f, 0.7f),
        new(0.8f, 0.5f, 0.2f, 0.7f),
        new(0.2f, 0.7f, 0.7f, 0.7f),
        new(0.9f, 0.6f, 0.3f, 0.7f),
        new(0.6f, 0.2f, 0.6f, 0.7f),
        new(0.3f, 0.6f, 0.2f, 0.7f),
    };

    // === 预案系统 ===
    [System.Serializable]
    class ConfigData
    {
        public string roomPrefabGUID;
        public List<string> obstaclePrefabGUIDs = new();
        public int selectedObstacleIndex;
        public float cellSize = 24f;
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

    [MenuItem("Tools/障碍物网格编辑器")]
    static void ShowWindow() => GetWindow<ObstacleGridEditor>("障碍物网格编辑器");

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

    static List<string> ToGUIDList(List<GameObject> objs)
    {
        var result = new List<string>();
        foreach (var o in objs)
            result.Add(o == null ? "" : ToGUID(o));
        return result;
    }

    static List<GameObject> FromGUIDList(List<string> guids)
    {
        var result = new List<GameObject>();
        if (guids == null) return result;
        foreach (var g in guids)
            result.Add(FromGUID<GameObject>(g));
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
        if (d == null) return;
        roomPrefab = FromGUID<GameObject>(d.roomPrefabGUID);
        obstaclePrefabs = FromGUIDList(d.obstaclePrefabGUIDs);
        selectedObstacleIndex = d.selectedObstacleIndex;
        cellSize = d.cellSize;
        needsRefresh = true;
    }

    ConfigData CollectFieldsToConfig()
    {
        return new ConfigData
        {
            roomPrefabGUID = ToGUID(roomPrefab),
            obstaclePrefabGUIDs = ToGUIDList(obstaclePrefabs),
            selectedObstacleIndex = selectedObstacleIndex,
            cellSize = cellSize
        };
    }

    // ──────────────────────────────────────────────
    //  数据刷新
    // ──────────────────────────────────────────────

    void RefreshData()
    {
        obstacleCellMap.Clear();
        enemySpawnCells.Clear();
        itemSpawnCells.Clear();

        if (roomPrefab == null) { needsRefresh = false; return; }

        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(roomPrefab));

        var roomRoot = root.GetComponent<RoomRoot>();
        if (roomRoot != null)
            roomSize = roomRoot.roomSize;

        cellOffsetX = (Mathf.RoundToInt(roomSize.x) % 2 == 0) ? 0.5f : 0f;
        cellOffsetY = (Mathf.RoundToInt(roomSize.y) % 2 == 0) ? 0.5f : 0f;

        var container = root.transform.Find("Obstacles");
        if (container != null)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                var cell = WorldToCell(child.localPosition);
                obstacleCellMap[cell] = -1;
            }
        }

        if (roomRoot != null)
        {
            if (roomRoot.enemySpawnPoints != null)
                foreach (var t in roomRoot.enemySpawnPoints)
                    if (t) enemySpawnCells.Add(WorldToCell(t.localPosition));
            if (roomRoot.itemSpawnPoints != null)
                foreach (var t in roomRoot.itemSpawnPoints)
                    if (t) itemSpawnCells.Add(WorldToCell(t.localPosition));
        }

        PrefabUtility.UnloadPrefabContents(root);
        needsRefresh = false;
    }

    Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x - cellOffsetX),
            Mathf.RoundToInt(worldPos.y - cellOffsetY));
    }

    Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x + cellOffsetX, cell.y + cellOffsetY, 0);
    }

    // ──────────────────────────────────────────────
    //  UI
    // ──────────────────────────────────────────────

    void OnGUI()
    {
        if (needsRefresh) RefreshData();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("障碍物网格编辑器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择房间预制体 → 在网格上放置障碍物/生成点 → 保存。\n" +
            "配置自动保存，关闭 Unity 不丢失。", MessageType.Info);

        DrawPresetSection();

        // ── 配置区 ──
        EditorGUILayout.Space(5);
        GUILayout.Label("配置", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        roomPrefab = (GameObject)EditorGUILayout.ObjectField("房间预制体", roomPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            needsRefresh = true;
            SaveCurrentToPresets();
            SavePresetsToDisk();
        }

        EditorGUILayout.LabelField("障碍物预制体:");
        for (int i = obstaclePrefabs.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = ObstacleColors[i % ObstacleColors.Length];
            GUILayout.Label($"[{i}]", GUILayout.Width(25));
            GUI.color = Color.white;

            obstaclePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(obstaclePrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("删", GUILayout.Width(30)))
            {
                obstaclePrefabs.RemoveAt(i);
                var toRemove = obstacleCellMap.Where(kv => kv.Value == i).Select(kv => kv.Key).ToList();
                foreach (var c in toRemove) obstacleCellMap.Remove(c);
                var toFix = obstacleCellMap.Where(kv => kv.Value > i).ToList();
                foreach (var kv in toFix) obstacleCellMap[kv.Key] = kv.Value - 1;

                if (selectedObstacleIndex >= obstaclePrefabs.Count)
                    selectedObstacleIndex = Mathf.Max(0, obstaclePrefabs.Count - 1);

                SaveCurrentToPresets();
                SavePresetsToDisk();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ 添加障碍物预制体"))
        {
            obstaclePrefabs.Add(null);
            SaveCurrentToPresets();
            SavePresetsToDisk();
        }

        if (obstaclePrefabs.Count > 0 && obstaclePrefabs.All(p => p != null))
        {
            string[] names = obstaclePrefabs.Select((p, i) => $"[{i}] {p.name}").ToArray();
            selectedObstacleIndex = EditorGUILayout.Popup("当前障碍物", selectedObstacleIndex, names);
        }

        cellSize = EditorGUILayout.Slider("格子像素大小", cellSize, 12f, 40f);

        EditorGUILayout.Space(10);

        bool canDrawGrid = roomPrefab != null;

        if (roomPrefab == null)
            EditorGUILayout.HelpBox("请选择房间预制体", MessageType.Info);

        // ── 编辑模式 ──
        EditorGUILayout.Space(3);
        GUILayout.Label("编辑模式", EditorStyles.miniBoldLabel);
        editMode = (EditMode)EditorGUILayout.EnumPopup("当前模式", editMode);

        bool obstacleMissing = obstaclePrefabs.Count == 0 || obstaclePrefabs.Any(p => p == null);

        switch (editMode)
        {
            case EditMode.Obstacle:
                if (obstacleMissing)
                    EditorGUILayout.HelpBox("请添加障碍物预制体", MessageType.Info);
                else
                    EditorGUILayout.HelpBox(
                        "点击空格放置当前选中类型的障碍物\n" +
                        "点击已有障碍物 → 替换为当前类型\n" +
                        "再次点击同类型障碍物 → 删除", MessageType.None);
                canDrawGrid = canDrawGrid && !obstacleMissing;
                break;
            case EditMode.EnemySpawn:
                EditorGUILayout.HelpBox("点击格子放置/删除敌人生成点", MessageType.None);
                break;
            case EditMode.ItemSpawn:
                EditorGUILayout.HelpBox("点击格子放置/删除道具生成点", MessageType.None);
                break;
        }

        if (canDrawGrid)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            DrawLegend(new Color(0.6f, 0.6f, 0.6f, 0.5f), "空格");
            DrawLegend(ObstacleColors[selectedObstacleIndex % ObstacleColors.Length], "障碍物");
            DrawLegend(new Color(0.2f, 0.5f, 0.9f, 0.7f), "敌人点");
            DrawLegend(new Color(0.7f, 0.3f, 0.9f, 0.7f), "道具点");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            DrawGrid();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存到预制体", GUILayout.Height(25)))
                EditorApplication.delayCall += () => SaveToPrefab();
            if (GUILayout.Button("刷新", GUILayout.Width(60)))
                needsRefresh = true;
            if (GUILayout.Button("清空当前模式", GUILayout.Width(100)))
            {
                ClearCurrentMode();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField(
            $"房间尺寸: {roomSize.x}×{roomSize.y} | " +
            $"障碍物: {obstacleCellMap.Count} | " +
            $"敌人点: {enemySpawnCells.Count} | " +
            $"道具点: {itemSpawnCells.Count}");
        {
            string info = hoverCell.HasValue
                ? $"当前格子: ({hoverCell.Value.x}, {hoverCell.Value.y})" +
                  (obstacleCellMap.TryGetValue(hoverCell.Value, out int idx) ? $" | 障碍物[{idx}]" : "")
                : "";
            EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            SaveCurrentToPresets();
            SavePresetsToDisk();
        }
    }

    Vector2 _scroll;

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

    void DrawGrid()
    {
        int cols = Mathf.RoundToInt(roomSize.x);
        int rows = Mathf.RoundToInt(roomSize.y);
        int halfW = Mathf.FloorToInt(roomSize.x * 0.5f);
        int halfH = Mathf.FloorToInt(roomSize.y * 0.5f);

        float gridW = cols * cellSize;
        float gridH = rows * cellSize;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos,
            GUILayout.Height(Mathf.Min(gridH + 40, 500)),
            GUILayout.MaxWidth(gridW + 30));

        EditorGUILayout.BeginVertical(GUILayout.Width(gridW));

        for (int row = rows - 1; row >= 0; row--)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(gridW));
            for (int col = 0; col < cols; col++)
            {
                var cell = new Vector2Int(col - halfW, row - halfH);

                Color bg;
                string label = "";

                if (obstacleCellMap.TryGetValue(cell, out int obsIdx))
                {
                    if (obsIdx < 0)
                    {
                        bg = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                        label = "×";
                    }
                    else
                    {
                        bg = ObstacleColors[obsIdx % ObstacleColors.Length];
                        label = obsIdx.ToString();
                    }
                }
                else if (enemySpawnCells.Contains(cell))
                {
                    bg = new Color(0.2f, 0.5f, 0.9f, 0.7f);
                    label = "E";
                }
                else if (itemSpawnCells.Contains(cell))
                {
                    bg = new Color(0.7f, 0.3f, 0.9f, 0.7f);
                    label = "I";
                }
                else
                    bg = new Color(0.6f, 0.6f, 0.6f, 0.3f);

                bool isHover = hoverCell.HasValue && hoverCell.Value == cell;
                if (isHover)
                    bg = Color.Lerp(bg, Color.yellow, 0.4f);

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = bg;

                if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                {
                    OnCellClicked(cell);
                    Repaint();
                }

                if (Event.current.type == EventType.Repaint)
                {
                    var btnRect = GUILayoutUtility.GetLastRect();
                    if (btnRect.Contains(Event.current.mousePosition))
                        hoverCell = cell;
                }

                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    void DrawLegend(Color color, string label)
    {
        var rect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
        EditorGUI.DrawRect(rect, color);
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(50));
    }

    // ──────────────────────────────────────────────
    //  交互
    // ──────────────────────────────────────────────

    void OnCellClicked(Vector2Int cell)
    {
        switch (editMode)
        {
            case EditMode.Obstacle:
                if (enemySpawnCells.Contains(cell)) return;
                if (itemSpawnCells.Contains(cell)) return;

                if (obstacleCellMap.TryGetValue(cell, out int existingIdx))
                {
                    if (existingIdx == selectedObstacleIndex)
                        // 同类型 → 删除
                        obstacleCellMap.Remove(cell);
                    else
                        // 不同类型(含-1原有障碍物) → 替换
                        obstacleCellMap[cell] = selectedObstacleIndex;
                }
                else
                    // 空格 → 放置
                    obstacleCellMap[cell] = selectedObstacleIndex;
                break;

            case EditMode.EnemySpawn:
                if (obstacleCellMap.ContainsKey(cell)) return;
                if (enemySpawnCells.Contains(cell))
                    enemySpawnCells.Remove(cell);
                else
                {
                    itemSpawnCells.Remove(cell);
                    enemySpawnCells.Add(cell);
                }
                break;

            case EditMode.ItemSpawn:
                if (obstacleCellMap.ContainsKey(cell)) return;
                if (itemSpawnCells.Contains(cell))
                    itemSpawnCells.Remove(cell);
                else
                {
                    enemySpawnCells.Remove(cell);
                    itemSpawnCells.Add(cell);
                }
                break;
        }
    }

    void ClearCurrentMode()
    {
        switch (editMode)
        {
            case EditMode.Obstacle: obstacleCellMap.Clear(); break;
            case EditMode.EnemySpawn: enemySpawnCells.Clear(); break;
            case EditMode.ItemSpawn: itemSpawnCells.Clear(); break;
        }
    }

    // ──────────────────────────────────────────────
    //  保存
    // ──────────────────────────────────────────────

    void SaveToPrefab()
    {
        if (roomPrefab == null) return;
        string path = AssetDatabase.GetAssetPath(roomPrefab);
        if (string.IsNullOrEmpty(path)) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        var roomRoot = root.GetComponent<RoomRoot>();

        // ── 障碍物 ──
        // 先收集原有障碍物（idx=-1）的 GO，保留它们
        var oldObs = root.transform.Find("Obstacles");
        List<GameObject> preservedObstacles = new();
        if (oldObs != null)
        {
            for (int i = 0; i < oldObs.childCount; i++)
            {
                preservedObstacles.Add(oldObs.GetChild(i).gameObject);
            }
        }

        // 重新创建容器
        if (oldObs != null)
        {
            // 把原有子物体先移出，再删旧容器
            foreach (var go in preservedObstacles)
                go.transform.SetParent(root.transform, true);
            DestroyImmediate(oldObs.gameObject);
        }

        var obsContainer = new GameObject("Obstacles").transform;
        obsContainer.SetParent(root.transform, false);
        obsContainer.localPosition = Vector3.zero;

        int placedCount = 0;
        foreach (var kv in obstacleCellMap)
        {
            var cell = kv.Key;
            int idx = kv.Value;

            if (idx == -1)
            {
                // 原有障碍物 → 找到对应的 GO 移回容器
                Vector3 worldPos = CellToWorld(cell);
                // 从 preservedObstacles 中找位置匹配的
                var match = preservedObstacles.FirstOrDefault(go =>
                    WorldToCell(go.transform.localPosition) == cell);
                if (match != null)
                {
                    match.transform.SetParent(obsContainer, false);
                    match.transform.localPosition = worldPos;
                    preservedObstacles.Remove(match);
                    placedCount++;
                }
                continue;
            }

            if (idx < 0 || idx >= obstaclePrefabs.Count || obstaclePrefabs[idx] == null) continue;

            var go2 = (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefabs[idx], obsContainer);
            go2.name = $"Obstacle_{idx}_{placedCount}";
            go2.transform.localPosition = CellToWorld(cell);
            placedCount++;
        }

        // 销毁未被匹配的原有障碍物（用户在网格中删除的 ×）
        foreach (var leftover in preservedObstacles)
            DestroyImmediate(leftover);

        // ── 敌人生成点 ──
        var oldEnemy = root.transform.Find("EnemySpawnPoints");
        if (oldEnemy != null)
            DestroyImmediate(oldEnemy.gameObject);

        var enemyContainer = new GameObject("EnemySpawnPoints").transform;
        enemyContainer.SetParent(root.transform, false);
        enemyContainer.localPosition = Vector3.zero;

        var enemyArr = new Transform[enemySpawnCells.Count];
        int ei = 0;
        foreach (var cell in enemySpawnCells)
        {
            var go = new GameObject("E");
            go.transform.SetParent(enemyContainer, false);
            go.transform.localPosition = CellToWorld(cell);
            enemyArr[ei++] = go.transform;
        }
        if (roomRoot != null)
            roomRoot.enemySpawnPoints = enemyArr;

        // ── 道具生成点 ──
        var oldItem = root.transform.Find("ItemSpawnPoints");
        if (oldItem != null)
            DestroyImmediate(oldItem.gameObject);

        var itemContainer = new GameObject("ItemSpawnPoints").transform;
        itemContainer.SetParent(root.transform, false);
        itemContainer.localPosition = Vector3.zero;

        var itemArr = new Transform[itemSpawnCells.Count];
        int ii = 0;
        foreach (var cell in itemSpawnCells)
        {
            var go = new GameObject("I");
            go.transform.SetParent(itemContainer, false);
            go.transform.localPosition = CellToWorld(cell);
            itemArr[ii++] = go.transform;
        }
        if (roomRoot != null)
            roomRoot.itemSpawnPoints = itemArr;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        needsRefresh = true;

        var typeCounts = obstacleCellMap.Values.Where(v => v >= 0).GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
        int preservedCount = obstacleCellMap.Count(kv => kv.Value == -1);
        string typeInfo = string.Join("\n", typeCounts.Select(kv => $"  [{kv.Key}] {obstaclePrefabs[kv.Key]?.name}: {kv.Value}个"));
        if (preservedCount > 0)
            typeInfo += $"\n  [×] 原有保留: {preservedCount}个";

        Debug.Log($"[ObstacleGridEditor] 已保存: {placedCount}障碍物, {enemySpawnCells.Count}敌人点, {itemSpawnCells.Count}道具点 → {path}");
        EditorUtility.DisplayDialog("完成",
            $"已保存到:\n{path}\n\n" +
            $"障碍物: {placedCount}\n{typeInfo}\n\n" +
            $"敌人生成点: {enemySpawnCells.Count}\n" +
            $"道具生成点: {itemSpawnCells.Count}", "确定");
    }

    static void Warn(string msg) => EditorUtility.DisplayDialog("提示", msg, "确定");
}
