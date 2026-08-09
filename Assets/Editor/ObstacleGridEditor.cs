using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 障碍物网格编辑器 — 2D 俯视网格，点击格子放置/删除障碍物。
/// 菜单: Tools > 障碍物网格编辑器
/// 保证障碍物位于格子正中心。
/// </summary>
public class ObstacleGridEditor : EditorWindow
{
    // === 配置 ===
    GameObject roomPrefab;
    List<GameObject> obstaclePrefabs = new();
    int selectedObstacleIndex = 0;
    float cellSize = 24f;

    // === 运行时数据 ===
    Vector2 roomSize = new(20, 12);
    float cellOffsetX = 0.5f;   // 偶数=0.5, 奇数=0
    float cellOffsetY = 0.5f;
    HashSet<Vector2Int> occupiedCells = new();
    HashSet<Vector2Int> enemySpawnCells = new();   // 敌人生成点（蓝色，不可放置）
    HashSet<Vector2Int> itemSpawnCells = new();    // 道具生成点（紫色，不可放置）
    bool needsRefresh = true;
    Vector2Int? hoverCell;
    Vector2 scrollPos;

    [MenuItem("Tools/障碍物网格编辑器")]
    static void ShowWindow() => GetWindow<ObstacleGridEditor>("障碍物网格编辑器");

    void OnEnable()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Obstacles" });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) obstaclePrefabs.Add(prefab);
        }
    }

    // ──────────────────────────────────────────────
    //  数据刷新
    // ──────────────────────────────────────────────

    void RefreshData()
    {
        occupiedCells.Clear();
        enemySpawnCells.Clear();
        itemSpawnCells.Clear();

        if (roomPrefab == null) { needsRefresh = false; return; }

        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(roomPrefab));

        var roomRoot = root.GetComponent<RoomRoot>();
        if (roomRoot != null)
            roomSize = roomRoot.roomSize;

        // 偶数尺寸格子中心在 n+0.5，奇数尺寸在 n
        cellOffsetX = (Mathf.RoundToInt(roomSize.x) % 2 == 0) ? 0.5f : 0f;
        cellOffsetY = (Mathf.RoundToInt(roomSize.y) % 2 == 0) ? 0.5f : 0f;

        var container = root.transform.Find("Obstacles");
        if (container != null)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                occupiedCells.Add(WorldToCell(child.localPosition));
            }
        }

        if (roomRoot != null)
        {
            // 生成点：不可放置
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

        // ── 配置区 ──
        EditorGUILayout.Space(5);
        GUILayout.Label("配置", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        roomPrefab = (GameObject)EditorGUILayout.ObjectField("房间预制体", roomPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
            needsRefresh = true;

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

        cellSize = EditorGUILayout.Slider("格子像素大小", cellSize, 12f, 40f);

        EditorGUILayout.Space(10);

        if (roomPrefab == null)
        {
            EditorGUILayout.HelpBox("请选择房间预制体", MessageType.Info);
            return;
        }
        if (obstaclePrefabs.Count == 0 || obstaclePrefabs.Any(p => p == null))
        {
            EditorGUILayout.HelpBox("请添加障碍物预制体", MessageType.Info);
            return;
        }

        // ── 图例 ──
        EditorGUILayout.BeginHorizontal();
        DrawLegend(new Color(0.6f, 0.6f, 0.6f, 0.5f), "空格");
        DrawLegend(new Color(0.2f, 0.8f, 0.2f, 0.7f), "障碍物");
        DrawLegend(new Color(0.2f, 0.5f, 0.9f, 0.5f), "敌人点");
        DrawLegend(new Color(0.7f, 0.3f, 0.9f, 0.5f), "道具点");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // ── 网格区 ──
        DrawGrid();

        // ── 操作按钮 ──
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存到预制体", GUILayout.Height(25)))
            SaveToPrefab();
        if (GUILayout.Button("刷新", GUILayout.Width(60)))
            needsRefresh = true;
        if (GUILayout.Button("清空全部", GUILayout.Width(80)))
        {
            occupiedCells.Clear();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"房间尺寸: {roomSize.x}×{roomSize.y} | 障碍物数量: {occupiedCells.Count}");
        if (hoverCell.HasValue)
            EditorGUILayout.LabelField($"当前格子: ({hoverCell.Value.x}, {hoverCell.Value.y})", EditorStyles.miniLabel);
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

        // 每行固定宽度，防止对齐错乱
        EditorGUILayout.BeginVertical(GUILayout.Width(gridW));

        for (int row = rows - 1; row >= 0; row--)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(gridW));
            for (int col = 0; col < cols; col++)
            {
                var cell = new Vector2Int(col - halfW, row - halfH);

                Color bg;
                if (occupiedCells.Contains(cell))
                    bg = new Color(0.2f, 0.8f, 0.2f, 0.7f);
                else if (enemySpawnCells.Contains(cell))
                    bg = new Color(0.2f, 0.5f, 0.9f, 0.5f);
                else if (itemSpawnCells.Contains(cell))
                    bg = new Color(0.7f, 0.3f, 0.9f, 0.5f);
                else
                    bg = new Color(0.6f, 0.6f, 0.6f, 0.3f);

                // hover 高亮
                bool isHover = hoverCell.HasValue && hoverCell.Value == cell;
                if (isHover && !enemySpawnCells.Contains(cell) && !itemSpawnCells.Contains(cell))
                    bg = Color.Lerp(bg, Color.yellow, 0.4f);

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = bg;

                string label = "";
                if (occupiedCells.Contains(cell)) label = "●";
                else if (enemySpawnCells.Contains(cell)) label = "E";
                else if (itemSpawnCells.Contains(cell)) label = "I";

                if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                {
                    OnCellClicked(cell);
                    Repaint();
                }

                // hover 检测
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
        if (enemySpawnCells.Contains(cell)) return;
        if (itemSpawnCells.Contains(cell)) return;

        if (occupiedCells.Contains(cell))
            occupiedCells.Remove(cell);
        else
            occupiedCells.Add(cell);
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

        var oldContainer = root.transform.Find("Obstacles");
        if (oldContainer != null)
            DestroyImmediate(oldContainer.gameObject);

        var container = new GameObject("Obstacles").transform;
        container.SetParent(root.transform, false);
        container.localPosition = Vector3.zero;

        int placedCount = 0;
        foreach (var cell in occupiedCells)
        {
            var prefab = obstaclePrefabs[selectedObstacleIndex % obstaclePrefabs.Count];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
            go.name = $"Obstacle_{placedCount}";
            go.transform.localPosition = CellToWorld(cell);
            placedCount++;
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        needsRefresh = true;

        Debug.Log($"[ObstacleGridEditor] 已保存 {placedCount} 个障碍物到 {path}");
        EditorUtility.DisplayDialog("完成", $"已保存 {placedCount} 个障碍物到:\n{path}", "确定");
    }
}
