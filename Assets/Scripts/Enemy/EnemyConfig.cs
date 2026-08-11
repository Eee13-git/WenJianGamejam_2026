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

    [Header("巡逻点（世界坐标相对室内/由生成器填充或运行时赋值）")]
    public List<Vector2> patrolPoints = new List<Vector2>();
}
