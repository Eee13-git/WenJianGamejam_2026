using UnityEngine;

/// <summary>
/// 移除品质效果 — 从道具池中永久移除指定品质的所有道具。
/// 本局内池空重置也不添加该品质道具，新一局清除。
/// 不可逆：失去道具时不恢复。
/// </summary>
[CreateAssetMenu(fileName = "RemoveQualityEffect", menuName = "Game/Item Effect/Remove Quality")]
public class RemoveQualityEffect : ItemEffectBase
{
    [Tooltip("要移除的品质")]
    [SerializeField] private ItemQuality _quality = ItemQuality.Common;

    public override void OnAcquire(GameObject owner)
    {
        ItemsLibrary.Instance?.BanQuality(_quality);
    }

    public override void OnRemove(GameObject owner)
    {
        // 不可逆
    }
}
