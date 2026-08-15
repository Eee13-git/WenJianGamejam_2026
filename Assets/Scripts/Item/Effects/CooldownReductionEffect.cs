using UnityEngine;

/// <summary>
/// 技能冷却缩减道具效果 — 与技能冷却乘算。
/// OnAcquire 时 SkillInstance.CooldownMultiplier *= factor，OnRemove 时 /= factor。
/// 每次获取叠加（乘算），可通过 maxCount 限制上限。
/// </summary>
[CreateAssetMenu(fileName = "CooldownReductionEffect", menuName = "Game/Item Effect/Cooldown Reduction")]
public class CooldownReductionEffect : ItemEffectBase
{
    [Tooltip("冷却乘区，0.85 = 减少15%冷却")]
    [SerializeField] private float _factor = 0.85f;

    public override void OnAcquire(GameObject owner)
    {
        if (_factor > 0f)
            SkillInstance.CooldownMultiplier *= _factor;
    }

    public override void OnRemove(GameObject owner)
    {
        if (_factor > 0f)
            SkillInstance.CooldownMultiplier /= _factor;
    }
}
