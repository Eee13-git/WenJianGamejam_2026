using UnityEngine;

/// <summary>
/// Buff 效果抽象基类 — 策略模式，定义 Buff 在生命周期各阶段的行为。
/// 每个具体效果是独立的 ScriptableObject 资产。
/// </summary>
public abstract class BuffEffectBase : ScriptableObject
{
    /// <summary>Buff 被应用到目标时调用（只调用一次）</summary>
    public abstract void OnApply(GameObject target, BuffInstance buff);

    /// <summary>Buff 被移除时调用（只调用一次）</summary>
    public abstract void OnRemove(GameObject target, BuffInstance buff);

    /// <summary>每帧 Tick（由 BuffManager 驱动），默认空实现</summary>
    public virtual void OnTick(GameObject target, BuffInstance buff, float deltaTime) { }
}
