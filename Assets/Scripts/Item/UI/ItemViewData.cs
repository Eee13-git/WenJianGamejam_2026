using System;
using UnityEngine;

/// <summary>
/// 传递给 ItemSlotView 的纯显示数据 DTO，无行为。
/// Controller 从 ItemManager/ItemData 提取后传入 View。
/// </summary>
public struct ItemViewData : IEquatable<ItemViewData>
{
    public Sprite Icon;
    public string Name;
    public string Description;
    public ItemQuality Quality;
    public int Count;

    public bool Equals(ItemViewData other)
    {
        return ReferenceEquals(Icon, other.Icon)
            && Name == other.Name
            && Description == other.Description
            && Quality == other.Quality
            && Count == other.Count;
    }

    public override bool Equals(object obj)
        => obj is ItemViewData other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + (Icon != null ? Icon.GetHashCode() : 0);
            hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
            hash = hash * 23 + (Description != null ? Description.GetHashCode() : 0);
            hash = hash * 23 + Quality.GetHashCode();
            hash = hash * 23 + Count.GetHashCode();
            return hash;
        }
    }
}
