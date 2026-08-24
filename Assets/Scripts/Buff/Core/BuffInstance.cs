using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 运行时实例 — 纯 C# class，非 MonoBehaviour。
/// 由 BuffManager 创建和管理生命周期。
/// 设计目标: 避免每个 Buff 创建 GameObject，统一由 BuffManager 的 Update 驱动 Tick。
/// </summary>
public class BuffInstance
{
    /// <summary>Buff 数据定义</summary>
    public BuffData Data { get; }

    /// <summary>挂载的目标 GameObject</summary>
    public GameObject Owner { get; }

    /// <summary>施放者（用于伤害归属等）</summary>
    public GameObject Caster { get; }

    /// <summary>当前堆叠数</summary>
    public int CurrentStacks { get; private set; } = 1;

    /// <summary>剩余持续时间（秒）</summary>
    public float RemainingDuration { get; private set; }

    /// <summary>已流逝时间（秒）</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>是否已过期</summary>
    public bool IsExpired => !Data.isPermanent && RemainingDuration <= 0f;

    /// <summary>是否活跃</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>不可清除标记（藏品/道具等来源的 buff，净化类技能不可清除）</summary>
    public bool Indestructible { get; set; }

    /// <summary>
    /// 每 tick 伤害覆盖值（用于技能区域按施法者攻击力缩放 DOT），-1 = 使用 BuffData 资产中的 damagePerTick。
    /// 由区域组件（GroundSolutionZone/PoisonGasZone/BloodPool 等）在 ApplyBuff 后设置。
    /// </summary>
    public float TickDamageOverride { get; set; } = -1f;

    /// <summary>
    /// 持续时间覆盖值（用于技能升级延长 buff 时长），-1 = 使用 BuffData 资产中的 duration。
    /// 设置后 RefreshDuration / 叠加刷新都使用覆盖值，保证升级时长在区域停留刷新后仍然生效。
    /// </summary>
    public float DurationOverride { get; private set; } = -1f;

    /// <summary>来源技能等级（默认1；供 Buff 效果按等级成长机制参数，如护盾量比例）</summary>
    public int SourceLevel { get; set; } = 1;

    /// <summary>独立计时模式下每个堆叠的剩余时间</summary>
    private readonly List<float> _stackTimers = new List<float>();

    public BuffInstance(BuffData data, GameObject owner, GameObject caster, int level = 1)
    {
        Data = data;
        Owner = owner;
        Caster = caster;
        SourceLevel = level;
        RemainingDuration = data.duration;
        _stackTimers.Add(data.duration);
    }

    /// <summary>设置持续时间覆盖值（同时立即应用到当前剩余时长），-1 = 恢复用资产值</summary>
    public void SetDurationOverride(float duration)
    {
        DurationOverride = duration >= 0f ? duration : -1f;
        RemainingDuration = DurationOverride >= 0f ? DurationOverride : Data.duration;
    }

    /// <summary>当前生效的持续时间（覆盖值或资产值）</summary>
    private float EffectiveDuration => DurationOverride >= 0f ? DurationOverride : Data.duration;

    /// <summary>添加堆叠</summary>
    public void AddStack()
    {
        if (!Data.stackable || CurrentStacks >= Data.maxStacks)
            return;

        switch (Data.stackBehavior)
        {
            case StackBehavior.Refresh:
                RemainingDuration = EffectiveDuration;
                break;
            case StackBehavior.Independent:
                _stackTimers.Add(EffectiveDuration);
                break;
            case StackBehavior.ExtendDuration:
                RemainingDuration += EffectiveDuration;
                break;
        }
        CurrentStacks++;
    }

    /// <summary>刷新持续时间到初始值（Refresh 行为；供"在圈内停留持续刷新 debuff"场景使用）</summary>
    public void RefreshDuration()
    {
        RemainingDuration = EffectiveDuration;
    }

    /// <summary>延长持续时间（ExtendDuration 行为）</summary>
    public void ExtendDuration(float amount)
    {
        RemainingDuration += amount;
    }

    /// <summary>直接设置剩余持续时间（偷取/转移 buff 等机制使用）</summary>
    public void SetRemainingDuration(float duration)
    {
        RemainingDuration = Mathf.Max(duration, 0f);
    }

    /// <summary>Tick — 由 BuffManager 调用</summary>
    public void Tick(float deltaTime)
    {
        if (!IsActive || Data.isPermanent)
            return;

        // Independent 模式：每帧 tick 所有独立堆叠
        if (Data.stackBehavior == StackBehavior.Independent)
        {
            for (int i = _stackTimers.Count - 1; i >= 0; i--)
            {
                _stackTimers[i] -= deltaTime;
                if (_stackTimers[i] <= 0f)
                {
                    _stackTimers.RemoveAt(i);
                    CurrentStacks--;
                }
            }
            RemainingDuration = _stackTimers.Count > 0 ? _stackTimers[^1] : -1f;
        }
        else
        {
            RemainingDuration -= deltaTime;
        }

        ElapsedTime += deltaTime;

        // 驱动效果 Tick
        Data.effect?.OnTick(Owner, this, deltaTime);
    }
}
