using UnityEngine;

/// <summary>
/// 降低品质权重效果 — 对指定品质的生成权重施加负偏移。
/// 最终权重 = 原始权重 - 降低量，clamp 到 ≥0。
/// 不可逆：失去道具时不恢复。
/// </summary>
[CreateAssetMenu(fileName = "ReduceQualityWeightEffect", menuName = "Game/Item Effect/Reduce Quality Weight")]
public class ReduceQualityWeightEffect : ItemEffectBase
{
    [Tooltip("要降低权重的品质")]
    [SerializeField] private ItemQuality _quality = ItemQuality.Uncommon;

    [Tooltip("权重降低量")]
    [SerializeField] private float _weightReduction = 20f;

    public override void OnAcquire(GameObject owner)
    {
        ItemsLibrary.Instance?.ApplyQualityWeightOffset(_quality, -_weightReduction);
    }

    public override void OnRemove(GameObject owner)
    {
        // 不可逆
    }
}
