using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

/// <summary>
/// 脓毒疮复合体 boss 预制体构建：
/// 以 巨噬细胞Boss.prefab 为模板克隆，替换 config/sprite/animator/phases/castDelay/BossLight 颜色，
/// 保存为 Assets/Prefabs/Boss/脓毒疮复合体boss.prefab，并注册到骨骼层 Boss 房间 enemyPool。
/// </summary>
public static class BuildPustuleBossPrefab
{
    private static string TemplatePath = "Assets/Prefabs/Boss/巨噬细胞Boss.prefab";
    private static string OutPath = "Assets/Prefabs/Boss/脓毒疮复合体boss.prefab";
    private static string ConfigPath = "Assets/Resources/Enemy/EnemyConfig_脓毒疮复合体boss.asset";
    private static string PhasePath = "Assets/Resources/Enemy/Boss/脓毒疮复合体bossPhase.asset";
    private static string ControllerPath = "Assets/Animations/Enemy/脓毒疮复合体boss/脓毒疮复合体boss_Controller.controller";
    private static string TexDir = "Assets/Textures/Enemy1.1/脓毒疮复合体boss/";
    private static string BossRoomConfig = "Assets/Resources/Map/RoomConfigs/骨骼层/RoomConfig_Room_Boss.asset";

    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(TemplatePath);
        try
        {
            root.name = "脓毒疮复合体boss";

            // EnemyCore.config（直接赋值，项目已验证此方式可持久化）
            var core = root.GetComponent<EnemyCore>();
            core.config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(ConfigPath);

            // SpriteRenderer：待机帧
            var sr = root.GetComponent<SpriteRenderer>();
            sr.sprite = FindSprite(TexDir + "脓毒疮复合体boss_idle.png", "脓毒疮复合体boss_idle");

            // Animator controller
            var anim = root.GetComponent<Animator>();
            anim.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            // EnemySkillManager._castDelay = 0.6（配合施法动画释放帧；10 帧@8fps，释放帧约 0.6s 开始）
            var skillMgr = root.GetComponent<EnemySkillManager>();
            SetPrivate(skillMgr, "_castDelay", 0.6f);

            // BossCore：phases + 空 stateManagedSkillIndices（BossCore 统一施放洪流）+ 血条 prefab（继承模板）
            var bossCore = root.GetComponent<BossCore>();
            var phase = AssetDatabase.LoadAssetAtPath<BossPhaseData>(PhasePath);
            SetPrivate(bossCore, "_phases", new[] { phase });
            SetPrivate(bossCore, "_stateManagedSkillIndices", new int[0]);

            // BossLight：黄绿色（与 boss 形象一致）
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "BossLight")
                {
                    var light = t.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                    if (light != null)
                        light.color = new Color(0.75f, 1f, 0.35f, 1f);
                }
            }

            if (File.Exists(OutPath)) AssetDatabase.DeleteAsset(OutPath);
            PrefabUtility.SaveAsPrefabAsset(root, OutPath);
            Debug.Log("[BuildPustuleBossPrefab] saved " + OutPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        RegisterInBossRoom();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildPustuleBossPrefab] done");
    }

    private static Sprite FindSprite(string sheetPath, string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (o is Sprite s && s.name == name) return s;
        return null;
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning("field not found: " + fieldName); return; }
        f.SetValue(target, value);
    }

    private static void RegisterInBossRoom()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<RoomConfig>(BossRoomConfig);
        if (cfg == null) { Debug.LogError("Boss room config not found: " + BossRoomConfig); return; }

        // 检查是否已注册
        foreach (var e in cfg.enemyPool)
            if (e != null && e.enemyPrefab != null && e.enemyPrefab.name == "脓毒疮复合体boss")
            {
                Debug.Log("[BuildPustuleBossPrefab] already in boss room pool");
                return;
            }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutPath);
        cfg.enemyPool.Add(new EnemySpawnEntry
        {
            enemyPrefab = prefab,
            minCount = 1,
            maxCount = 1,
            weight = 10f
        });
        EditorUtility.SetDirty(cfg);
        Debug.Log("[BuildPustuleBossPrefab] registered in " + BossRoomConfig);
    }
}
