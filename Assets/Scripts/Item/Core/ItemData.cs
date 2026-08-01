using UnityEngine;

/// <summary>
/// 道具数据资产（ScriptableObject）— 定义一种道具的所有静态属性。
/// 右键 -> Create -> Game -> Item Data 创建。
/// 参考: 以撒的结合道具 / 崩坏星穹铁道奇物 / 明日方舟藏品。
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("唯一标识，如 'blood_of_martyr'")]
    public string itemId;              // 唯一标识
    public string itemName;            // 显示名称
    [TextArea] public string description;
    public Sprite icon;
    [Tooltip("品质: Common/Uncommon/Rare/Legendary")]
    public ItemQuality quality;        // 品质

    [Header("效果（策略模式）")]
    [Tooltip("拖入具体效果资产: ItemEffectBase 子类")]
    public ItemEffectBase effect;      // 道具效果策略

    [Header("获取限制")]
    [Tooltip("可获取的数量上限。0 = 无限制，>0 = 最多持有该数量")]
    [Min(0)] public int maxCount;     // 数量上限
}
