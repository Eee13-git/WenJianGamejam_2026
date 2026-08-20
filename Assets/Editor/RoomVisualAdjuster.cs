using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 房间视觉元素调整器 — 在不改变障碍物/生成点的前提下，批量修改墙壁、墙角、地板、门的缩放和偏移。
/// 菜单: Tools > 房间视觉调整器
/// </summary>
public class RoomVisualAdjuster : EditorWindow
{
    private GameObject _roomPrefab;
    private Vector2 _floorScale = Vector2.one;
    private Vector2 _floorOffset = Vector2.zero;
    private Vector2 _wallScale = Vector2.one;
    private Vector2 _wallOffset = Vector2.zero;
    private Vector2 _doorScale = Vector2.one;
    private Vector2 _doorOffset = Vector2.zero;

    private bool _floorFoldout = true;
    private bool _wallFoldout = true;
    private bool _doorFoldout = true;

    private Vector2 _scroll;

    [MenuItem("Tools/房间视觉调整器")]
    static void ShowWindow() => GetWindow<RoomVisualAdjuster>("房间视觉调整器");

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("房间视觉调整器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择房间预制体 → 调整缩放/偏移 → 应用。\n" +
            "仅修改墙壁/墙角/地板/门的视觉 transform，不影响障碍物和生成点。\n" +
            "缩放为倍率（1=原始），偏移为世界单位增量。",
            MessageType.Info);

        // 预制体选择
        EditorGUI.BeginChangeCheck();
        _roomPrefab = (GameObject)EditorGUILayout.ObjectField(
            "房间预制体", _roomPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        if (_roomPrefab == null)
        {
            EditorGUILayout.EndScrollView();
            return;
        }

        // 状态信息
        int floorCount = CountChildrenByPrefix(_roomPrefab.transform, "Floor", "Floor_");
        int wallCount = CountWallChildren(_roomPrefab.transform);
        int doorCount = CountChildrenByPrefix(_roomPrefab.transform, "Doors", "Door_");

        EditorGUILayout.LabelField($"检测到: 地板×{floorCount}  墙壁×{wallCount}  门×{doorCount}",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // 地板
        _floorFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_floorFoldout, "地板");
        if (_floorFoldout)
        {
            _floorScale = EditorGUILayout.Vector2Field("缩放倍率 (X=横向, Y=纵向)", _floorScale);
            _floorOffset = EditorGUILayout.Vector2Field("偏移 (X=横向, Y=纵向)", _floorOffset);
            if (GUILayout.Button("应用地板", GUILayout.Width(100)))
                ApplyToCategory("Floor", _floorScale, _floorOffset);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();

        // 墙壁（含墙角）
        _wallFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_wallFoldout, "墙壁 (含墙角)");
        if (_wallFoldout)
        {
            _wallScale = EditorGUILayout.Vector2Field("缩放倍率 (X=横向, Y=纵向)", _wallScale);
            _wallOffset = EditorGUILayout.Vector2Field("偏移 (X=横向, Y=纵向)", _wallOffset);
            if (GUILayout.Button("应用墙壁", GUILayout.Width(100)))
                ApplyToCategory("Walls", _wallScale, _wallOffset);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();

        // 门
        _doorFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_doorFoldout, "门");
        if (_doorFoldout)
        {
            _doorScale = EditorGUILayout.Vector2Field("缩放倍率 (X=横向, Y=纵向)", _doorScale);
            _doorOffset = EditorGUILayout.Vector2Field("偏移 (X=横向, Y=纵向)", _doorOffset);
            if (GUILayout.Button("应用门", GUILayout.Width(100)))
                ApplyToCategory("Doors", _doorScale, _doorOffset);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(20);

        // 全部应用
        if (GUILayout.Button("全部应用", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认", "将应用全部缩放和偏移到房间预制体，是否继续？", "确认", "取消"))
            {
                ApplyToCategory("Floor", _floorScale, _floorOffset);
                ApplyToCategory("Walls", _wallScale, _wallOffset);
                ApplyToCategory("Doors", _doorScale, _doorOffset);
            }
        }

        // 重置按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("重置参数", GUILayout.Width(100)))
        {
            _floorScale = _wallScale = _doorScale = Vector2.one;
            _floorOffset = _wallOffset = _doorOffset = Vector2.zero;
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>应用缩放和偏移到指定类别</summary>
    private void ApplyToCategory(string category, Vector2 scale, Vector2 offset)
    {
        if (_roomPrefab == null) return;

        string path = AssetDatabase.GetAssetPath(_roomPrefab);
        if (string.IsNullOrEmpty(path)) return;

        var root = PrefabUtility.LoadPrefabContents(path);

        Transform container = root.transform.Find(category);
        if (container == null)
        {
            container = root.transform;
        }

        int modified = 0;

        // Floor 特殊处理：容器自身可能有 SpriteRenderer（单块地板）
        if (category == "Floor" && container.GetComponent<SpriteRenderer>() != null)
        {
            ApplyTransform(container, scale, offset);
            modified++;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            string name = child.name;

            bool match = category switch
            {
                "Floor" => name.StartsWith("Floor_") || name == "Floor",
                "Walls" => name.StartsWith("Wall_") || name.StartsWith("Corner_"),
                "Doors" => name.StartsWith("Door_"),
                _ => false
            };

            if (!match) continue;

            ApplyTransform(child, scale, offset);
            modified++;
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[RoomVisualAdjuster] {category}: 修改了 {modified} 个元素 (scale={scale} offset={offset})");
        ShowNotification(new GUIContent($"{category}: {modified} 个元素已修改"));
    }

    /// <summary>统计地板数量（单块或格子拼贴）</summary>
    private int CountChildrenByPrefix(Transform root, string containerName, string prefix)
    {
        var container = root.Find(containerName);
        if (container == null) return 0;

        // 单块地板：容器自身有 SpriteRenderer
        if (container.GetComponent<SpriteRenderer>() != null && container.childCount == 0)
            return 1;

        int count = 0;
        for (int i = 0; i < container.childCount; i++)
        {
            if (container.GetChild(i).name.StartsWith(prefix))
                count++;
        }
        return count;
    }

    /// <summary>统计墙壁子物体数量（Wall_* + Corner_*）</summary>
    private int CountWallChildren(Transform root)
    {
        var container = root.Find("Walls");
        if (container == null) return 0;
        int count = 0;
        for (int i = 0; i < container.childCount; i++)
        {
            string name = container.GetChild(i).name;
            if (name.StartsWith("Wall_") || name.StartsWith("Corner_"))
                count++;
        }
        return count;
    }

    /// <summary>对单个 Transform 应用缩放倍率和偏移</summary>
    private void ApplyTransform(Transform t, Vector2 scale, Vector2 offset)
    {
        t.localScale = new Vector3(
            t.localScale.x * scale.x,
            t.localScale.y * scale.y,
            t.localScale.z);

        t.localPosition = new Vector3(
            t.localPosition.x + offset.x,
            t.localPosition.y + offset.y,
            t.localPosition.z);
    }
}
