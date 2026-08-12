using UnityEngine;

/// <summary>
/// 品质权重 — 配置某品质的生成概率权重。
/// 用于 ItemsLibrary 全局默认权重和 RoomConfig 按房间类型覆盖。
/// </summary>
[System.Serializable]
public struct QualityWeight
{
    [Tooltip("品质等级")]
    public ItemQuality quality;

    [Tooltip("生成权重（相对值，如 Common=50, Legendary=5）")]
    [Range(0f, 100f)]
    public float weight;
}
