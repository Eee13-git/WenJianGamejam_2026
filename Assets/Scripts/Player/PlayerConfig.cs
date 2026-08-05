using UnityEngine;

/// <summary>
/// 玩家初始配置 (ScriptableObject) — 定义 Player 的最基础数值属性。
/// PlayerManager 动态生成 Player 时，以此为基础值，再叠加科技树加成。
/// 右键 -> Create -> Player -> Player Config 创建。
/// </summary>
[CreateAssetMenu(menuName = "Player/Player Config", fileName = "PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    [Header("生命属性")]
    [Tooltip("最大生命值")]
    public float maxHealth = 10000f;

    [Header("移动属性")]
    [Tooltip("移动速度")]
    public float moveSpeed = 5f;

    [Header("攻击属性")]
    [Tooltip("攻击力")]
    public float attackStrength = 20f;

    [Header("射击属性")]
    [Tooltip("子弹速度")]
    public float bulletSpeed = 10f;
    [Tooltip("每分钟射击次数")]
    public float shotsPerMinute = 90f;

    [Header("碰撞属性")]
    [Tooltip("碰撞半径")]
    public float colliderRadius = 0.4f;
}
