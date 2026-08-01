using UnityEngine;

/// <summary>
/// Buff 数据资产（ScriptableObject）— 定义一种 Buff 的所有静态属性。
/// 右键 -> Create -> Game -> Buff Data 创建。
/// </summary>
[CreateAssetMenu(fileName = "NewBuff", menuName = "Game/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("唯一标识，如 'poison_debuff'")]
    public string buffId;              // 唯一标识
    public string buffName;            // 显示名称
    [TextArea] public string description;
    public Sprite icon;
    public BuffType buffType;          // Buff / Debuff / Neutral

    [Header("时间配置")]
    [Tooltip("持续时间（秒），0 = 永久")]
    public float duration;             // 持续时间（秒）
    [Tooltip("是否永久 Buff（不受时间影响）")]
    public bool isPermanent;           // 是否永久 Buff

    [Header("叠加配置")]
    [Tooltip("是否允许叠加")]
    public bool stackable;             // 是否可叠加
    [Tooltip("最大堆叠数")]
    public int maxStacks = 1;          // 最大堆叠数
    [Tooltip("叠加行为: Refresh / Independent / ExtendDuration")]
    public StackBehavior stackBehavior; // 叠加行为

    [Header("效果策略")]
    [Tooltip("具体效果策略，拖入 BuffEffectBase 子类资产")]
    public BuffEffectBase effect;      // 具体效果策略
}
