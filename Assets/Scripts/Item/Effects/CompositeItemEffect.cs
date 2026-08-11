using UnityEngine;

/// <summary>
/// 复合道具效果 — 让一个道具拥有多个效果/上多个Buff。
/// OnAcquire 顺序执行所有子效果，OnRemove 逆序还原。
/// </summary>
[CreateAssetMenu(fileName = "CompositeItemEffect", menuName = "Game/Item Effect/Composite")]
public class CompositeItemEffect : ItemEffectBase
{
    [SerializeField] private ItemEffectBase[] _effects;

    public override void OnAcquire(GameObject owner)
    {
        if (_effects == null) return;
        foreach (var effect in _effects)
            effect?.OnAcquire(owner);
    }

    public override void OnRemove(GameObject owner)
    {
        if (_effects == null) return;
        // 逆序还原（后挂的 Buff 先移除）
        for (int i = _effects.Length - 1; i >= 0; i--)
            _effects[i]?.OnRemove(owner);
    }
}
