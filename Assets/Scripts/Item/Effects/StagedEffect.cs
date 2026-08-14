using UnityEngine;

/// <summary>
/// 阶段道具效果 — 根据持有该道具的数量执行对应阶段的效果。
/// 每个阶段是一个 ItemEffectBase（可以是 CompositeItemEffect、MultiStatModifierEffect 等）。
/// 超过最大阶段数后循环：第4个重新触发第1阶段效果，第5个触发第2阶段，以此类推。
/// OnAcquire 时按数组索引取模执行对应阶段；OnRemove 时倒序（LIFO）还原。
/// </summary>
[CreateAssetMenu(fileName = "StagedEffect", menuName = "Game/Item Effect/Staged")]
public class StagedEffect : ItemEffectBase
{
    [System.Serializable]
    public struct Stage
    {
        [Tooltip("此阶段需要的道具数量（从1开始，仅做标记，实际按数组顺序循环）")]
        public int requiredCount;
        [Tooltip("此阶段的效果（到达时执行，移除时还原）")]
        public ItemEffectBase effect;
    }

    [SerializeField] private Stage[] _stages;

    [Tooltip("道具 itemId，用于查询持有数量")]
    [SerializeField] private string _itemId = "atropine";

    public override void OnAcquire(GameObject owner)
    {
        if (_stages == null || _stages.Length == 0) return;

        var itemManager = owner.GetComponent<ItemManager>();
        if (itemManager == null) return;

        // 获取前的数量（0-indexed），取模实现循环
        int count = itemManager.GetItemCount(_itemId);
        int stageIndex = count % _stages.Length;
        _stages[stageIndex].effect?.OnAcquire(owner);
    }

    public override void OnRemove(GameObject owner)
    {
        if (_stages == null || _stages.Length == 0) return;

        var itemManager = owner.GetComponent<ItemManager>();
        if (itemManager == null) return;

        // 移除前的数量，倒序移除（LIFO）
        int count = itemManager.GetItemCount(_itemId);
        int stageIndex = ((count - 1) % _stages.Length + _stages.Length) % _stages.Length;
        _stages[stageIndex].effect?.OnRemove(owner);
    }
}
