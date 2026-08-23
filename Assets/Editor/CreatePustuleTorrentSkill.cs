using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// 脓疮洪流技能资产创建：
/// - Resources/Skills/Data/脓疮洪流/TorrentEffect.asset（TorrentSkillEffect，配置帧动画视觉）
/// - Resources/Skills/Data/脓疮洪流/PustuleTorrent.asset（SkillData: skillId=pustule_torrent）
/// - Resources/Skills/脓毒疮复合体bossSkillLibrary.asset（含 pustule_torrent）
/// TorrentSkillEffect 私有字段用反射赋值（项目惯例）。
/// </summary>
public static class CreatePustuleTorrentSkill
{
    private static string DataDir = "Assets/Resources/Skills/Data/脓疮洪流/";
    private static string SkillRoot = "Assets/Resources/Skills/";
    private static string TexDir = "Assets/Textures/Enemy1.1/脓毒疮复合体boss/";

    public static void Run()
    {
        CreateTorrentEffect();
        CreateSkillData();
        CreateSkillLibrary();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreatePustuleTorrentSkill] done");
    }

    private static Sprite[] LoadTorrentSprites()
    {
        var list = new List<Sprite>();
        var all = AssetDatabase.LoadAllAssetsAtPath(TexDir + "脓疮复合体洪流_frames.png");
        for (int i = 0; i < 4; i++)
        {
            foreach (var o in all)
            {
                if (o is Sprite s && s.name == $"脓疮复合体洪流_{i}")
                {
                    list.Add(s);
                    break;
                }
            }
        }
        return list.ToArray();
    }

    private static Sprite LoadIcon()
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(DataDir + "Icon.png"))
            if (o is Sprite s) return s;
        return null;
    }

    private static void CreateTorrentEffect()
    {
        if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        string path = DataDir + "TorrentEffect.asset";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        var effect = ScriptableObject.CreateInstance<TorrentSkillEffect>();
        var type = typeof(TorrentSkillEffect);
        // 私有 [SerializeField] 字段用反射赋值
        SetField(type, effect, "_duration", 3.5f);
        SetField(type, effect, "_range", 9f);
        SetField(type, effect, "_spreadAngle", 110f);
        SetField(type, effect, "_damagePerSecond", 30f);
        SetField(type, effect, "_damageInterval", 0.2f);
        SetField(type, effect, "_knockbackSpeed", 0f);            // Boss 喷射期间原地锁定
        SetField(type, effect, "_torrentMaterial", null);
        SetField(type, effect, "_animationSprites", LoadTorrentSprites());
        SetField(type, effect, "_animationFps", 8f);

        AssetDatabase.CreateAsset(effect, path);
        EditorUtility.SetDirty(effect);
        Debug.Log("[CreatePustuleTorrentSkill] created " + path);
    }

    private static void CreateSkillData()
    {
        string path = DataDir + "PustuleTorrent.asset";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        var data = ScriptableObject.CreateInstance<SkillData>();
        data.skillId = "pustule_torrent";
        data.skillName = "脓疮洪流";
        data.description = "向前方喷涌大范围脓疮洪流，对路径上所有敌人持续造成伤害。";
        data.icon = LoadIcon();
        data.cooldown = 6f;
        data.castRange = 10f;
        data.damageMultiplier = 1f;
        data.maxLevel = 1;
        data.passive = false;
        data.skillEffect = AssetDatabase.LoadAssetAtPath<TorrentSkillEffect>(DataDir + "TorrentEffect.asset");

        AssetDatabase.CreateAsset(data, path);
        EditorUtility.SetDirty(data);
        Debug.Log("[CreatePustuleTorrentSkill] created " + path);
    }

    private static void CreateSkillLibrary()
    {
        string path = SkillRoot + "脓毒疮复合体bossSkillLibrary.asset";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        var lib = ScriptableObject.CreateInstance<SkillLibrary>();
        var skillData = AssetDatabase.LoadAssetAtPath<SkillData>(DataDir + "PustuleTorrent.asset");
        var field = typeof(SkillLibrary).GetField("_allSkills", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = new List<SkillData> { skillData };
        field.SetValue(lib, list);

        AssetDatabase.CreateAsset(lib, path);
        EditorUtility.SetDirty(lib);
        Debug.Log("[CreatePustuleTorrentSkill] created " + path);
    }

    private static void SetField(System.Type type, object target, string name, object value)
    {
        var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning("field not found: " + name); return; }
        f.SetValue(target, value);
    }
}
