using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Enemy/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("显示名称（用于结算UI等），为空时使用资产文件名")]
    public string displayName;

    [Header("基本数值")]
    public float maxHealth = 30f;
    [Tooltip("碰撞伤害值")]
    public float contactDamage = 10f;
    [Tooltip("碰撞伤害冷却（秒）")]
    public float contactDamageCooldown = 1f;

    [Header("移动速度")]
    [Tooltip("巡逻时的移动速度")]
    public float patrolSpeed = 1f;
    [Tooltip("追击时的移动速度")]
    public float chaseSpeed = 2.5f;

    [Header("感知")]
    public float detectionRange = 5f;
    public float attackRange = 1.2f;

    [Header("技能")]
    public SkillLibrary skillLibrary;

    [Tooltip("固定技能 ID 列表（非空则按此列表精确装配，否则从 skillLibrary 随机抽取）")]
    public List<string> fixedSkillIds = new List<string>();

    [Header("轴向喷射（结核分枝杆菌等）")]
    [Tooltip("为 true 时，敌人检测到玩家在同一行/同一列即沿该轴喷射技能，而非靠近攻击")]
    public bool useAxialSpray = false;

    [Tooltip("同行/同列判定阈值（世界单位）")]
    public float axialAlignThreshold = 0.6f;

    [Header("随机技能（多技能敌人）")]
    [Tooltip("为 true 时，攻击时从就绪技能中随机选择释放（概率发动多技能），而非固定释放 index 0")]
    public bool useRandomSkill = false;

    [Header("巡逻点（世界坐标相对室内/由生成器填充或运行时赋值）")]
    public List<Vector2> patrolPoints = new List<Vector2>();
}
