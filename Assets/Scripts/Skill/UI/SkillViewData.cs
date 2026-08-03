using System;
using UnityEngine;

/// <summary>
/// 传递给 SkillSlotView 的纯显示数据 DTO，无行为。
/// Controller 从 SkillInstance / SkillSlot 提取后传入 View。
/// </summary>
public struct SkillViewData : IEquatable<SkillViewData>
{
    public Sprite Icon;
    public string KeyLabel;
    public bool ShowKeyLabel;
    public int Level;
    public int MaxLevel;
    public bool IsEquipped;
    public bool IsUnlocked;
    public bool IsCoolingDown;
    public float CooldownPercent;
    public float CooldownRemaining;

    public bool Equals(SkillViewData other)
    {
        return ReferenceEquals(Icon, other.Icon)
            && KeyLabel == other.KeyLabel
            && Level == other.Level
            && MaxLevel == other.MaxLevel
            && IsEquipped == other.IsEquipped
            && IsUnlocked == other.IsUnlocked
            && IsCoolingDown == other.IsCoolingDown
            && ShowKeyLabel == other.ShowKeyLabel
            && Mathf.Approximately(CooldownPercent, other.CooldownPercent)
            && Mathf.Approximately(CooldownRemaining, other.CooldownRemaining);
    }

    public override bool Equals(object obj) => obj is SkillViewData other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + (Icon != null ? Icon.GetHashCode() : 0);
            hash = hash * 23 + (KeyLabel != null ? KeyLabel.GetHashCode() : 0);
            hash = hash * 23 + Level.GetHashCode();
            hash = hash * 23 + MaxLevel.GetHashCode();
            hash = hash * 23 + IsEquipped.GetHashCode();
            hash = hash * 23 + IsUnlocked.GetHashCode();
            hash = hash * 23 + IsCoolingDown.GetHashCode();
            hash = hash * 23 + CooldownPercent.GetHashCode();
            hash = hash * 23 + CooldownRemaining.GetHashCode();
            return hash;
        }
    }
}
