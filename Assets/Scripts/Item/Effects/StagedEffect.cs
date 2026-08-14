using UnityEngine;

/// <summary>
/// 阶段道具效果 — 根据持有该道具的数量执行对应阶段的效果。
/// 每个阶段是一个 ItemEffectBase（可以是 CompositeItemEffect、MultiStatModifierEffect 等）。
/// OnAcquire 时根据当前数量执行对应阶段效果；OnRemove 时逆序还原对应阶段。
/// </summary>
[CreateAssetMenu(fileName = "StagedEffect", menuName = "Game/Item Effect/Staged")]
public class StagedEffect : ItemEffectBase
{
    [System.Serializable]
    public struct Stage
    {
        [Tooltip("此阶段需要的道具数量（从1开始）")]
        public int requiredCount;
        [Tooltip("此阶段的效果（到达时执行，移除时还原）")]
        public ItemEffectBase effect;
    }

    [SerializeField] private Stage[] _stages;

    [Tooltip("道具 itemId，用于查询持有数量")]
    [SerializeField] private string _itemId = "atropine";

    public override void OnAcquire(GameObject owner)
    {
        if (_stages == null) return;

        var itemManager = owner.GetComponent<ItemManager>();
        if (itemManager == null) return;

        // 获取前的数量 = 当前阶段索引（0=还没第一个）
        int count = itemManager.GetItemCount(_itemId);

        // 找到 requiredCount == count+1 的阶段（本次获取后变为 count+1 个）
        foreach (var stage in _stages)
        {
            if (stage.requiredCount == count + 1)
            {
                stage.effect?.OnAcquire(owner);
                return;
            }
        }
    }

    public override void OnRemove(GameObject owner)
    {
        if (_stages == null) return;

        var itemManager = owner.GetComponent<ItemManager>();
        if (itemManager == null) return;

        // 移除前的数量 = 当前阶段
        int count = itemManager.GetItemCount(_itemId);

        // 找到 requiredCount == count 的阶段（移除后变为 count-1 个）
        foreach (var stage in _stages)
        {
            if (stage.requiredCount == count)
            {
                stage.effect?.OnRemove(owner);
                return;
            }
        }
    }
}
