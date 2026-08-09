using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 障碍物 Scene 编辑工具 — 在 Scene View 中点击格子放置/删除障碍物。
/// 菜单: Tools > 障碍物 Scene 编辑
/// 左键放置，右键删除，保证格子正中心对齐。
/// </summary>
public class ObstacleSceneTool : EditorWindow
{
    static bool enabled = false;

    GameObject roomPrefab;
    List<GameObject> obstaclePrefabs = new();
    int selectedObstacleIndex = 0;

    // 运行时缓存（避免每帧 LoadPrefabContents）
    GameObject _cachedRoot;
    string _cachedPath;
    Vector2 _roomSize = new(20, 12);
    float _cellOffsetX = 0.5f;   // 偶数=0.5, 奇数=0
    float _cellOffsetY = 0.5f;
    HashSet<Vector2Int> _forbiddenCells = new();

    [MenuItem("Tools/障碍物 Scene 编辑")]
    static void ShowWindow()
    {
        var wnd = GetWindow<ObstacleSceneTool>("障碍物 Scene 编辑");
        enabled = true;
        SceneView.duringSceneGui += wnd.OnSceneGUI;
    }

    void OnEnable()
    {
        // 默认加载障碍物预制体
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Obstacles" });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) obstaclePrefabs.Add(prefab);
        }
    }

    void OnDestroy()
    {
        Disable();
    }

    void Disable()
    {
        enabled = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        // 清理缓存
        if (_cachedRoot != null && !string.IsNullOrEmpty(_cachedPath))
        {
            PrefabUtility.UnloadPrefabContents(_cachedRoot);
            _cachedRoot = null;
        }
        SceneView.RepaintAll();
    }

    // ──────────────────────────────────────────────
    //  UI
    // ──────────────────────────────────────────────

    void OnGUI()
    {
        GUILayout.Label("障碍物 Scene 编辑", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 选择目标房间预制体\n" +
            "2. 点击「开启 Scene 编辑」\n" +
            "3. 在 Scene View 中:\n" +
            "   左键点击空格 → 放置障碍物\n" +
            "   左键点击已有障碍物 → 删除\n" +
            "   右键 → 删除障碍物\n" +
            "4. 点击「保存到预制体」保存修改\n" +
            "所有障碍物自动对齐到格子正中心。",
            MessageType.Info);

        EditorGUILayout.Space(5);
        roomPrefab = (GameObject)EditorGUILayout.ObjectField("房间预制体", roomPrefab, typeof(GameObject), false);

        // 障碍物预制体列表
        EditorGUILayout.LabelField("障碍物预制体:");
        for (int i = obstaclePrefabs.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            obstaclePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(obstaclePrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("删", GUILayout.Width(30)))
            {
                obstaclePrefabs.RemoveAt(i);
                if (selectedObstacleIndex >= obstaclePrefabs.Count)
                    selectedObstacleIndex = Mathf.Max(0, obstaclePrefabs.Count - 1);
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ 添加障碍物预制体"))
            obstaclePrefabs.Add(null);

        if (obstaclePrefabs.Count > 0 && obstaclePrefabs.All(p => p != null))
        {
            string[] names = obstaclePrefabs.Select(p => p.name).ToArray();
            selectedObstacleIndex = EditorGUILayout.Popup("当前障碍物", selectedObstacleIndex, names);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (!enabled)
        {
            if (GUILayout.Button("开启 Scene 编辑", GUILayout.Height(30)))
            {
                if (roomPrefab == null) { EditorUtility.DisplayDialog("提示", "请选择房间预制体", "确定"); return; }
                if (obstaclePrefabs.Count == 0 || obstaclePrefabs.Any(p => p == null))
                { EditorUtility.DisplayDialog("提示", "请添加障碍物预制体", "确定"); return; }

                enabled = true;
                SceneView.duringSceneGui += OnSceneGUI;
                CacheRoomData();
                SceneView.RepaintAll();
            }
        }
        else
        {
            if (GUILayout.Button("关闭 Scene 编辑", GUILayout.Height(30)))
                Disable();
        }

        if (GUILayout.Button("保存到预制体", GUILayout.Height(30)))
            SaveToPrefab();

        if (GUILayout.Button("刷新缓存", GUILayout.Height(30), GUILayout.Width(80)))
            CacheRoomData();
        EditorGUILayout.EndHorizontal();

        if (enabled && _cachedRoot != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"房间: {_cachedPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"房间尺寸: {_roomSize.x}×{_roomSize.y}", EditorStyles.miniLabel);

            // 统计当前障碍物数量
            var container = _cachedRoot.transform.Find("Obstacles");
            int count = container != null ? container.childCount : 0;
            EditorGUILayout.LabelField($"障碍物数量: {count}", EditorStyles.miniLabel);
        }
    }

    // ──────────────────────────────────────────────
    //  缓存房间数据
    // ──────────────────────────────────────────────

    void CacheRoomData()
    {
        if (roomPrefab == null) return;
        string path = AssetDatabase.GetAssetPath(roomPrefab);
        if (string.IsNullOrEmpty(path)) return;

        // 清理旧缓存
        if (_cachedRoot != null && !string.IsNullOrEmpty(_cachedPath))
            PrefabUtility.UnloadPrefabContents(_cachedRoot);

        _cachedPath = path;
        _cachedRoot = PrefabUtility.LoadPrefabContents(path);
        _forbiddenCells.Clear();

        var roomRoot = _cachedRoot.GetComponent<RoomRoot>();
        if (roomRoot != null)
        {
            _roomSize = roomRoot.roomSize;
            _cellOffsetX = (Mathf.RoundToInt(_roomSize.x) % 2 == 0) ? 0.5f : 0f;
            _cellOffsetY = (Mathf.RoundToInt(_roomSize.y) % 2 == 0) ? 0.5f : 0f;
            if (roomRoot.topDoor) _forbiddenCells.Add(WorldToCell(roomRoot.topDoor.localPosition));
            if (roomRoot.bottomDoor) _forbiddenCells.Add(WorldToCell(roomRoot.bottomDoor.localPosition));
            if (roomRoot.leftDoor) _forbiddenCells.Add(WorldToCell(roomRoot.leftDoor.localPosition));
            if (roomRoot.rightDoor) _forbiddenCells.Add(WorldToCell(roomRoot.rightDoor.localPosition));
            if (roomRoot.enemySpawnPoints != null)
                foreach (var t in roomRoot.enemySpawnPoints)
                    if (t) _forbiddenCells.Add(WorldToCell(t.localPosition));
            if (roomRoot.itemSpawnPoints != null)
                foreach (var t in roomRoot.itemSpawnPoints)
                    if (t) _forbiddenCells.Add(WorldToCell(t.localPosition));
        }

        SceneView.RepaintAll();
    }

    // ──────────────────────────────────────────────
    //  坐标转换
    // ──────────────────────────────────────────────

    Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x - _cellOffsetX),
            Mathf.RoundToInt(worldPos.y - _cellOffsetY));
    }

    Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x + _cellOffsetX, cell.y + _cellOffsetY, 0);
    }

    // ──────────────────────────────────────────────
    //  Scene GUI
    // ──────────────────────────────────────────────

    void OnSceneGUI(SceneView sceneView)
    {
        if (!enabled || _cachedRoot == null) return;

        Event e = Event.current;

        // 绘制网格
        DrawGrid(sceneView);

        // 绘制禁放格子
        foreach (var cell in _forbiddenCells)
        {
            Vector3 center = CellToWorld(cell);
            Handles.color = new Color(0.8f, 0.2f, 0.2f, 0.4f);
            Handles.DrawSolidRectangleWithOutline(
                new Rect(center.x - 0.5f, center.y - 0.5f, 1f, 1f),
                new Color(0.8f, 0.2f, 0.2f, 0.2f),
                new Color(0.8f, 0.2f, 0.2f, 0.6f));
        }

        // 标记已有障碍物
        var container = _cachedRoot.transform.Find("Obstacles");
        if (container != null)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                Vector3 center = child.localPosition;
                Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
                Handles.DrawWireCube(center, new Vector3(0.9f, 0.9f, 0));
            }
        }

        // 鼠标悬停高亮
        Vector2Int? hoverCell = GetMouseCell(e, sceneView);
        if (hoverCell.HasValue)
        {
            Vector3 center = CellToWorld(hoverCell.Value);
            Handles.color = Color.yellow;
            Handles.DrawWireCube(center, new Vector3(1f, 1f, 0));

            // 显示坐标
            Handles.Label(center + Vector3.up * 0.6f, $"({hoverCell.Value.x}, {hoverCell.Value.y})");
        }

        // 处理点击
        if (e.type == EventType.MouseDown && !e.alt)
        {
            if (hoverCell.HasValue)
            {
                if (e.button == 0) // 左键
                {
                    ToggleObstacle(hoverCell.Value);
                    e.Use();
                }
                else if (e.button == 1) // 右键
                {
                    RemoveObstacle(hoverCell.Value);
                    e.Use();
                }
            }
        }
    }

    void DrawGrid(SceneView sceneView)
    {
        int cols = Mathf.RoundToInt(_roomSize.x);
        int rows = Mathf.RoundToInt(_roomSize.y);
        int halfW = Mathf.FloorToInt(_roomSize.x * 0.5f);
        int halfH = Mathf.FloorToInt(_roomSize.y * 0.5f);

        // 格子中心偏移：偶数=n+0.5（边界在整数），奇数=n（边界在 n±0.5）
        float gridOffsetX = _cellOffsetX - 0.5f;  // 偶数=0, 奇数=-0.5
        float gridOffsetY = _cellOffsetY - 0.5f;

        float startX = -halfW + gridOffsetX;
        float endX = startX + cols;
        float startY = -halfH + gridOffsetY;
        float endY = startY + rows;

        Handles.color = new Color(1f, 1f, 1f, 0.2f);

        // 垂直线
        for (int i = 0; i <= cols; i++)
        {
            float x = startX + i;
            Handles.DrawLine(new Vector3(x, startY, 0), new Vector3(x, endY, 0));
        }

        // 水平线
        for (int i = 0; i <= rows; i++)
        {
            float y = startY + i;
            Handles.DrawLine(new Vector3(startX, y, 0), new Vector3(endX, y, 0));
        }

        // 房间边界
        Handles.color = new Color(1f, 1f, 1f, 0.5f);
        Handles.DrawLine(new Vector3(startX, startY, 0), new Vector3(endX, startY, 0));
        Handles.DrawLine(new Vector3(startX, endY, 0), new Vector3(endX, endY, 0));
        Handles.DrawLine(new Vector3(startX, startY, 0), new Vector3(startX, endY, 0));
        Handles.DrawLine(new Vector3(endX, startY, 0), new Vector3(endX, endY, 0));
    }

    Vector2Int? GetMouseCell(Event e, SceneView sceneView)
    {
        if (e == null) return null;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        // 投影到 z=0 平面
        float t = -ray.origin.z / ray.direction.z;
        if (t < 0) return null;
        Vector3 worldPos = ray.origin + ray.direction * t;

        return WorldToCell(worldPos);
    }

    // ──────────────────────────────────────────────
    //  障碍物操作
    // ──────────────────────────────────────────────

    void ToggleObstacle(Vector2Int cell)
    {
        if (_forbiddenCells.Contains(cell)) return;

        var container = _cachedRoot.transform.Find("Obstacles");
        if (container == null)
        {
            var go = new GameObject("Obstacles");
            go.transform.SetParent(_cachedRoot.transform, false);
            go.transform.localPosition = Vector3.zero;
            container = go.transform;
        }

        // 检查是否已有障碍物
        Vector3 worldPos = CellToWorld(cell);
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (Vector3.Distance(child.localPosition, worldPos) < 0.1f)
            {
                // 已有 → 删除
                Undo.DestroyObjectImmediate(child.gameObject);
                SceneView.RepaintAll();
                return;
            }
        }

        // 放置新障碍物
        var prefab = obstaclePrefabs[selectedObstacleIndex % obstaclePrefabs.Count];
        var obstacle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
        obstacle.name = $"Obstacle";
        obstacle.transform.localPosition = worldPos;
        Undo.RegisterCreatedObjectUndo(obstacle, "Place Obstacle");
        SceneView.RepaintAll();
    }

    void RemoveObstacle(Vector2Int cell)
    {
        var container = _cachedRoot.transform.Find("Obstacles");
        if (container == null) return;

        Vector3 worldPos = CellToWorld(cell);
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (Vector3.Distance(child.localPosition, worldPos) < 0.1f)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                SceneView.RepaintAll();
                return;
            }
        }
    }

    // ──────────────────────────────────────────────
    //  保存
    // ──────────────────────────────────────────────

    void SaveToPrefab()
    {
        if (_cachedRoot == null || string.IsNullOrEmpty(_cachedPath)) return;

        // 重命名障碍物（按序号）
        var container = _cachedRoot.transform.Find("Obstacles");
        if (container != null)
        {
            int i = 0;
            for (int c = 0; c < container.childCount; c++)
                container.GetChild(c).name = $"Obstacle_{i++}";
        }

        PrefabUtility.SaveAsPrefabAsset(_cachedRoot, _cachedPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ShowNotification(new GUIContent($"已保存到 {_cachedPath}"));
        Debug.Log($"[ObstacleSceneTool] 已保存到 {_cachedPath}");
    }
}
