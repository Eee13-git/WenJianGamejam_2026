using UnityEngine;

/// <summary>
/// 技能数据资产（ScriptableObject）。
/// 右键 -> Create -> Game -> Skill Data 创建。
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "Game/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("基本信息")]
    public string skillId;          // 唯一标识，如 "fireball"
    public string skillName;        // 显示名称
    [TextArea] public string description;
    public Sprite icon;

    [Header("数值")]
    public float cooldown = 2f;           // 基础冷却时间（秒）
    public float castRange = 10f;         // 施法距离
    public float damageMultiplier = 1.5f; // 伤害系数（乘以施法者攻击力）
    public int maxLevel = 5;              // 最大升级等级

    [Header("表现")]
    public GameObject projectilePrefab;   // 投射物预制体（可为空，旧方式兼容）

    [Header("效果策略（优先于 projectilePrefab）")]
    public SkillEffectBase skillEffect;   // 拖入具体效果资产，如 ProjectileSkillEffect
}
