using UnityEngine;

/// <summary>
/// 道具效果抽象基类 — 策略模式，定义道具被获得/移除时的行为。
/// 与 SkillEffectBase 类似，每个具体效果是独立的 ScriptableObject 资产。
///
/// 子类:
///   StatModifierEffect — 直接修改属性
///   ApplyBuffEffect    — 挂载 Buff（道具系统与 Buff 系统的主要桥接）
///   ConditionalEffect  — 条件触发效果
/// </summary>
public abstract class ItemEffectBase : ScriptableObject
{
    /// <summary>道具被获得时调用</summary>
    public abstract void OnAcquire(GameObject owner);

    /// <summary>道具被移除时调用（清理副作用）</summary>
    public abstract void OnRemove(GameObject owner);
}
