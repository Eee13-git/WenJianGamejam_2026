using System;
using UnityEngine;

/// <summary>
/// 技能运行时实例，持有 SkillData + 等级 + 冷却状态。
/// 通过 OnExecute 委托注入具体释放效果。
/// </summary>
public class SkillInstance
{
    /// <summary>全局技能冷却乘区（道具/buff 效果），1=正常，0.8=减少20%冷却</summary>
    public static float CooldownMultiplier = 1f;

    /// <summary>技能释放时跳过冷却的概率（钙调蛋白等效果），0=正常</summary>
    public static float CooldownSkipChance = 0f;

    /// <summary>当前是否处于技能伤害执行中（蛋白激酶等"技能命中附带伤害"使用）</summary>
    public static bool SkillDamageActive = false;

    /// <summary>实例级冷却乘区（随从技能CD缩减 IL-2 等效果），1=正常</summary>
    public float CooldownFactor = 1f;

    public SkillData Data { get; private set; }
    public int Level { get; private set; } = 1;
    public int MaxLevel => Data.maxLevel;
    public bool IsMaxLevel => Level >= MaxLevel;
    public bool IsCoolingDown { get; private set; }

    private float _cooldownRemaining;

    /// <summary>剩余冷却秒数（供 UI 读取）</summary>
    public float CooldownRemaining => _cooldownRemaining;

    /// <summary>冷却进度 0~1，1=满冷却（刚释放），0=冷却完毕（供 UI 读取）</summary>
    public float CooldownPercent
    {
        get
        {
            float total = CurrentCooldown;
            return total > 0f ? Mathf.Clamp01(_cooldownRemaining / total) : 0f;
        }
    }

    /// <summary>当前等级冷却时间（每级减少10%，再乘全局CD乘区和实例CD乘区）</summary>
    public float CurrentCooldown => Data.cooldown * (1f - (Level - 1) * 0.1f) * CooldownMultiplier * CooldownFactor;

    /// <summary>当前等级伤害系数</summary>
    public float CurrentDamageMultiplier => Data.damageMultiplier * (1f + (Level - 1) * 0.15f);

    /// <summary>技能执行委托：参数为 (施法者, 目标方向)</summary>
    public event Action<ISkillCaster, Vector2> OnExecute;

    internal SkillInstance(SkillData data)
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

    /// <summary>尝试释放技能，返回是否成功（立即执行效果）</summary>
    public bool TryCast(ISkillCaster caster, Vector2 targetDirection)
    {
        if (IsCoolingDown) return false;

        // 钙调蛋白：概率跳过冷却
        if (UnityEngine.Random.value >= CooldownSkipChance)
            StartCooldown();

        ExecuteEffect(caster, targetDirection);
        return true;
    }

    /// <summary>开始冷却（不执行效果）</summary>
    public void StartCooldown()
    {
        IsCoolingDown = true;
        _cooldownRemaining = CurrentCooldown;
    }

    /// <summary>执行技能效果（调用 OnExecute 委托）</summary>
    public void ExecuteEffect(ISkillCaster caster, Vector2 targetDirection)
    {
        OnExecute?.Invoke(caster, targetDirection);
    }

    /// <summary>升级技能，返回是否成功</summary>
    public bool TryUpgrade()
    {
        if (IsMaxLevel) return false;
        Level++;
        return true;
    }

    /// <summary>立即刷新冷却（完美格挡等效果触发时使用）</summary>
    public void ResetCooldown()
    {
        IsCoolingDown = false;
        _cooldownRemaining = 0f;
    }
}
