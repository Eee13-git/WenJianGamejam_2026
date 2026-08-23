using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 脓毒疮复合体 boss 配置与阶段数据创建/更新：
/// - 更新 EnemyConfig_脓毒疮复合体boss.asset（Boss 数值 + 专属技能库 + fixedSkillIds）
/// - 创建 BossPhaseData 脓毒疮复合体bossPhase.asset（技能=pustule_torrent）
/// </summary>
public static class SetupPustuleBossConfig
{
    private static string ConfigPath = "Assets/Resources/Enemy/EnemyConfig_脓毒疮复合体boss.asset";
    private static string PhasePath = "Assets/Resources/Enemy/Boss/脓毒疮复合体bossPhase.asset";
    private static string LibraryPath = "Assets/Resources/Skills/脓毒疮复合体bossSkillLibrary.asset";
    private static string SkillDataPath = "Assets/Resources/Skills/Data/脓疮洪流/PustuleTorrent.asset";

    public static void Run()
    {
        UpdateConfig();
        CreatePhase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPustuleBossConfig] done");
    }

    private static void UpdateConfig()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>(ConfigPath);
        if (cfg == null) { Debug.LogError("config not found"); return; }

        var lib = AssetDatabase.LoadAssetAtPath<SkillLibrary>(LibraryPath);
        var skillData = AssetDatabase.LoadAssetAtPath<SkillData>(SkillDataPath);

        cfg.displayName = "脓毒疮复合体boss";
        cfg.maxHealth = 400f;
        cfg.contactDamage = 15f;
        cfg.contactDamageCooldown = 1f;
        cfg.patrolSpeed = 1f;
        cfg.chaseSpeed = 2.6f;
        cfg.detectionRange = 12f;
        cfg.attackRange = 1.5f;
        cfg.skillLibrary = lib;
        cfg.fixedSkillIds = new List<string> { "pustule_torrent" };
        cfg.useAxialSpray = false;
        cfg.axialAlignThreshold = 0.6f;
        cfg.useRandomSkill = false;
        cfg.useBossLunge = false;

        EditorUtility.SetDirty(cfg);
        Debug.Log("[SetupPustuleBossConfig] config updated: " + ConfigPath);
    }

    private static void CreatePhase()
    {
        if (File.Exists(PhasePath)) AssetDatabase.DeleteAsset(PhasePath);

        var phase = ScriptableObject.CreateInstance<BossPhaseData>();
        phase.phaseName = "一阶段";
        phase.skills = new[] { AssetDatabase.LoadAssetAtPath<SkillData>(SkillDataPath) };
        phase.skillInterval = 5f;
        phase.moveSpeedMult = 1f;
        phase.attackIntervalMult = 1f;
        phase.healthThreshold = 1f;

        AssetDatabase.CreateAsset(phase, PhasePath);
        EditorUtility.SetDirty(phase);
        Debug.Log("[SetupPustuleBossConfig] phase created: " + PhasePath);
    }
}
