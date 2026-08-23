using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 将 脓毒疮复合体boss 注册到所有层的 Boss 房间 enemyPool（骨骼层已注册，补齐其余层）。
/// </summary>
public static class RegisterPustuleBossToRooms
{
    private static string BossPrefabPath = "Assets/Prefabs/Boss/脓毒疮复合体boss.prefab";

    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        if (prefab == null) { Debug.LogError("boss prefab not found"); return; }

        string root = "Assets/Resources/Map/RoomConfigs";
        string[] layers = { "骨骼层", "血管层", "神经层", "皮肤层", "淋巴层" };

        foreach (var layer in layers)
        {
            string path = $"{root}/{layer}/RoomConfig_Room_Boss.asset";
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path)))
            {
                Debug.Log($"skip missing: {path}");
                continue;
            }
            var cfg = AssetDatabase.LoadAssetAtPath<RoomConfig>(path);
            if (cfg == null) { Debug.LogError("load fail: " + path); continue; }

            bool exists = false;
            foreach (var e in cfg.enemyPool)
                if (e != null && e.enemyPrefab != null && e.enemyPrefab.name == "脓毒疮复合体boss") { exists = true; break; }

            if (exists)
            {
                Debug.Log($"already registered: {layer}");
                continue;
            }

            cfg.enemyPool.Add(new EnemySpawnEntry
            {
                enemyPrefab = prefab,
                minCount = 1,
                maxCount = 1,
                weight = 10f
            });
            EditorUtility.SetDirty(cfg);
            Debug.Log($"registered: {layer}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RegisterPustuleBossToRooms] done");
    }
}
