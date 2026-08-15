using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 科技树机制门效果 — 通过枚举选择机制类型。
/// 数值节点用 StatBonusEffect，机制门用此类。
/// </summary>
[CreateAssetMenu(menuName = "TechTree/Mechanism Effect", fileName = "MechanismEffect")]
public class TechTreeMechanismEffect : TechTreeEffectBase
{
    public enum MechanismType
    {
        /// <summary>技能槽位+1</summary>
        ExpandSkillSlot,
        /// <summary>随从数量上限+1</summary>
        FollowerSlotPlus,
        /// <summary>商店95折（全局折扣+5%）</summary>
        ShopDiscount5,
        /// <summary>ATP获取+5%</summary>
        AtpGainPlus5
    }

    [SerializeField] private MechanismType _mechanism;

    public MechanismType Mechanism => _mechanism;

    public override void ApplyBonus(Dictionary<string, float> bonuses)
    {
        // 机制效果不在 bonuses 字典中体现，通过 ApplyMechanism 单独触发
    }

    /// <summary>应用机制效果（由 TechTreeManager 在解锁时调用）</summary>
    public void ApplyMechanism()
    {
        switch (_mechanism)
        {
            case MechanismType.ExpandSkillSlot:
                // 需要在 PlayerSkillManager 就绪后调用
                // 用标记延迟到 PlayerManager.OnPlayerReady
                _pendingSkillSlotExpand = true;
                break;
            case MechanismType.FollowerSlotPlus:
                EnemyFollower.MaxFollowerCount += 1;
                break;
            case MechanismType.ShopDiscount5:
                ShopManager.GlobalDiscount += 0.05f;
                break;
            case MechanismType.AtpGainPlus5:
                CurrencyManager.GainMultiplier *= 1.05f;
                break;
        }
    }

    /// <summary>检查是否有待执行的技能槽扩展</summary>
    private static bool _pendingSkillSlotExpand;

    public static bool ConsumePendingSkillSlotExpand()
    {
        if (_pendingSkillSlotExpand)
        {
            _pendingSkillSlotExpand = false;
            return true;
        }
        return false;
    }

    public override string GetEffectSummary()
    {
        return _mechanism switch
        {
            MechanismType.ExpandSkillSlot => "技能槽位 +1",
            MechanismType.FollowerSlotPlus => "随从数量上限 +1",
            MechanismType.ShopDiscount5 => "商店 95 折",
            MechanismType.AtpGainPlus5 => "ATP 获取 +5%",
            _ => _mechanism.ToString()
        };
    }
}
