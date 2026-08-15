using UnityEngine;

/// <summary>
/// 技能冷却缩减 Buff — 与技能冷却乘算。
/// OnApply 时 SkillInstance.CooldownMultiplier *= factor，OnRemove 时 /= factor。
/// </summary>
[CreateAssetMenu(fileName = "CooldownReductionBuff", menuName = "Game/Buff Effect/Cooldown Reduction")]
public class CooldownReductionBuff : BuffEffectBase
{
    [Tooltip("冷却乘区，0.8 = 减少20%冷却")]
    [SerializeField] private float _factor = 0.8f;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        if (_factor > 0f)
            SkillInstance.CooldownMultiplier *= _factor;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (_factor > 0f)
            SkillInstance.CooldownMultiplier /= _factor;
    }
}
