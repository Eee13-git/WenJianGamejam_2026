using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 科技树效果策略基类 (ScriptableObject) — 类似 SkillEffectBase。
/// 每种效果类型继承此类，实现 ApplyBonus 和 GetEffectSummary。
/// 效果资产在 Inspector 中拖入 TechTreeNodeData.effect 字段。
/// </summary>
public abstract class TechTreeEffectBase : ScriptableObject
{
    /// <summary>将加成写入字典（供 PlayerStats.ApplyBonuses 使用）</summary>
    public abstract void ApplyBonus(Dictionary<string, float> bonuses);

    /// <summary>返回效果摘要文本（用于 UI 显示）</summary>
    public abstract string GetEffectSummary();
}
