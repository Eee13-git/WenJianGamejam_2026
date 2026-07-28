using System;
using UnityEngine;

/// <summary>
/// 技能运行时实例，持有 SkillData + 等级 + 冷却状态。
/// 通过 OnExecute 委托注入具体释放效果。
/// </summary>
public class SkillInstance
{
    public SkillData Data { get; private set; }
    public int Level { get; private set; } = 1;
    public int MaxLevel => Data.maxLevel;
    public bool IsMaxLevel => Level >= MaxLevel;
    public bool IsCoolingDown { get; private set; }

    private float _cooldownRemaining;

    /// <summary>当前等级冷却时间（每级减少10%）</summary>
    public float CurrentCooldown => Data.cooldown * (1f - (Level - 1) * 0.1f);

    /// <summary>当前等级伤害系数</summary>
    public float CurrentDamageMultiplier => Data.damageMultiplier * (1f + (Level - 1) * 0.15f);

    /// <summary>技能执行委托：参数为 (施法者, 目标方向)</summary>
    public event Action<ISkillCaster, Vector2> OnExecute;

    public SkillInstance(SkillData data)
    {
        Data = data;
        Level = 1;
    }

    /// <summary>每帧驱动冷却</summary>
    public void TickCooldown(float deltaTime)
    {
        if (!IsCoolingDown) return;
        _cooldownRemaining -= deltaTime;
        if (_cooldownRemaining <= 0f)
        {
            IsCoolingDown = false;
            _cooldownRemaining = 0f;
        }
    }

    /// <summary>尝试释放技能，返回是否成功</summary>
    public bool TryCast(ISkillCaster caster, Vector2 targetDirection)
    {
        if (IsCoolingDown) return false;

        IsCoolingDown = true;
        _cooldownRemaining = CurrentCooldown;

        OnExecute?.Invoke(caster, targetDirection);
        return true;
    }

    /// <summary>升级技能，返回是否成功</summary>
    public bool TryUpgrade()
    {
        if (IsMaxLevel) return false;
        Level++;
        return true;
    }
}
