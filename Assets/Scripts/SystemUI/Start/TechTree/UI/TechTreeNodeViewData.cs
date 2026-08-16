using UnityEngine;

/// <summary>
/// 科技树节点视图数据 (DTO) — 从 Model 构建传递给 View 的不可变数据。
/// 类似 SkillViewData，实现 IEquatable 用于变化检测。
/// </summary>
public struct TechTreeNodeViewData : System.IEquatable<TechTreeNodeViewData>
{
    public enum NodeType { Normal, Mechanism, StatMechanism }

    public string NodeId;
    public string DisplayName;
    public string Description;
    public string EffectSummary;
    public int Cost;
    public bool IsUnlocked;
    public bool CanUnlock;
    public int CurrentTechPoints;
    public NodeType Type;

    public bool Equals(TechTreeNodeViewData other)
    {
        return NodeId == other.NodeId
            && DisplayName == other.DisplayName
            && Description == other.Description
            && EffectSummary == other.EffectSummary
            && Cost == other.Cost
            && IsUnlocked == other.IsUnlocked
            && CanUnlock == other.CanUnlock
            && CurrentTechPoints == other.CurrentTechPoints
            && Type == other.Type;
    }

    public override bool Equals(object obj) => obj is TechTreeNodeViewData other && Equals(other);
    public override int GetHashCode() => NodeId?.GetHashCode() ?? 0;
}
