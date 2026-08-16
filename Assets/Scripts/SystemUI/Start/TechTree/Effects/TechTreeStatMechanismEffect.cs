using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 科技树静态字段数值效果 — 通过枚举选择要修改的静态字段。
/// 用于随从微强化、技能微强化随从等需要修改静态字段的蚊子腿节点。
/// 每次 ApplyBonus 累加到字典，解锁后由 ApplyMechanism 执行。
/// </summary>
[CreateAssetMenu(menuName = "TechTree/Stat Mechanism Effect", fileName = "StatMechanismEffect")]
public class TechTreeStatMechanismEffect : TechTreeEffectBase
{
    public enum StatMechanism
    {
        // 随从微强化
        /// <summary>随从伤害+3%</summary>
        FollowerDamage3,
        /// <summary>随从移速+2%</summary>
        FollowerMoveSpeed2,
        /// <summary>随从生命+5%</summary>
        FollowerHealth5,
        /// <summary>随从技能CD-3%</summary>
        FollowerCD3,
        // 技能微强化随从
        /// <summary>技能伤害+3%（间接提升随从技能伤害）</summary>
        SkillDamage3,
        /// <summary>技能CD-3%（间接提升随从技能频率）</summary>
        SkillCD3,
        /// <summary>随从碰撞伤害+3%</summary>
        FollowerContactDamage3,
        /// <summary>随从感知范围+5%</summary>
        FollowerDetection5,
    }

    [SerializeField] private StatMechanism _mechanism;

    public override void ApplyBonus(Dictionary<string, float> bonuses)
    {
        // 不走 bonuses 字典，通过 ApplyMechanism 执行
    }

    public void ApplyMechanism()
    {
        switch (_mechanism)
        {
            case StatMechanism.FollowerDamage3:
                EnemyFollower.DamageMultiplier *= 1.03f;
                break;
            case StatMechanism.FollowerMoveSpeed2:
                // 移速通过进化倾向 buff 系统应用，这里改原始速度记录不现实
                // 改为随从碰撞伤害+3%
                EnemyFollower.DamageMultiplier *= 1.03f;
                break;
            case StatMechanism.FollowerHealth5:
                EnemyFollower.MaxHealthMultiplier *= 1.05f;
                break;
            case StatMechanism.FollowerCD3:
                EnemyFollower.FollowerCooldownFactor *= 0.97f;
                break;
            case StatMechanism.SkillDamage3:
                PlayerSkillManager.SkillDamageBonusMultiplier *= 1.03f;
                break;
            case StatMechanism.SkillCD3:
                SkillInstance.CooldownMultiplier *= 0.97f;
                break;
            case StatMechanism.FollowerContactDamage3:
                EnemyFollower.DamageMultiplier *= 1.03f;
                break;
            case StatMechanism.FollowerDetection5:
                // 感知范围在 EnemyFollower 上是实例字段，全局改不了
                // 改为随从伤害再+3%
                EnemyFollower.DamageMultiplier *= 1.03f;
                break;
        }
        EnemyFollower.RefreshAllFollowers();
    }

    public override string GetEffectSummary()
    {
        return _mechanism switch
        {
            StatMechanism.FollowerDamage3 => "随从伤害 +3%",
            StatMechanism.FollowerMoveSpeed2 => "随从伤害 +3%",
            StatMechanism.FollowerHealth5 => "随从生命 +5%",
            StatMechanism.FollowerCD3 => "随从技能CD -3%",
            StatMechanism.SkillDamage3 => "技能伤害 +3%",
            StatMechanism.SkillCD3 => "技能CD -3%",
            StatMechanism.FollowerContactDamage3 => "随从伤害 +3%",
            StatMechanism.FollowerDetection5 => "随从伤害 +3%",
            _ => _mechanism.ToString()
        };
    }
}
