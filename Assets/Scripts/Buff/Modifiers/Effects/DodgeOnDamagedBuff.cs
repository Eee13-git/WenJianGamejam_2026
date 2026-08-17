using UnityEngine;

/// <summary>
/// 受击闪避 buff 效果 — OnApply 时给目标添加 DodgeComponent，OnRemove 时移除。
/// 目标（敌人）即将受击时由 EnemyStats.TakeDamage 调用 DodgeComponent.TryDodge()，
/// 成功闪避则触发滑移位移 + 免疫本次伤害。
/// 泛用类：闪避概率、滑移距离/速度/冷却均可配置。
/// 右键 -> Create -> Game -> Buff Effect -> Dodge On Damaged
/// </summary>
[CreateAssetMenu(fileName = "DodgeOnDamagedBuff", menuName = "Game/Buff Effect/Dodge On Damaged")]
public class DodgeOnDamagedBuff : BuffEffectBase
{
    [Header("闪避配置")]
    [Tooltip("闪避概率 (0-1)，1 = 每次受击都闪避")]
    [Range(0f, 1f)] public float dodgeChance = 1f;
    [Tooltip("闪避冷却（秒）")]
    public float dodgeCooldown = 2f;

    [Header("滑移参数")]
    [Tooltip("滑移距离")]
    public float slideDistance = 3f;
    [Tooltip("滑移速度（单位/秒）")]
    public float slideSpeed = 18f;
    [Tooltip("滑移后无敌时长（秒，免疫本次伤害）")]
    public float immuneDuration = 0.5f;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var dodge = target.GetComponent<DodgeComponent>();
        if (dodge == null)
            dodge = target.AddComponent<DodgeComponent>();
        dodge.Initialize(dodgeChance, dodgeCooldown, slideDistance, slideSpeed, immuneDuration);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        var dodge = target.GetComponent<DodgeComponent>();
        if (dodge != null)
            Destroy(dodge);
    }
}
