using UnityEngine;

/// <summary>
/// 净化道具效果 — 移除指定的 Buff 并永久免疫该 Buff。
/// OnAcquire: 移除已存在的对应 Buff + 添加免疫。
/// OnRemove: 移除免疫（一次性净化不恢复已移除的 Buff）。
/// </summary>
[CreateAssetMenu(fileName = "PurifyBuffEffect", menuName = "Game/Item Effect/Purify Buff")]
public class PurifyBuffEffect : ItemEffectBase
{
    [Tooltip("要净化并免疫的 Buff ID 列表")]
    [SerializeField] private string[] _buffIds;

    public override void OnAcquire(GameObject owner)
    {
        var buffManager = owner.GetComponent<BuffManager>();
        if (buffManager == null) return;

        foreach (var buffId in _buffIds)
        {
            // 移除已存在的 Buff
            buffManager.RemoveBuffById(buffId);
            // 添加免疫，阻止后续施加
            buffManager.AddBuffImmunity(buffId);
        }
    }

    public override void OnRemove(GameObject owner)
    {
        var buffManager = owner.GetComponent<BuffManager>();
        if (buffManager == null) return;

        foreach (var buffId in _buffIds)
            buffManager.RemoveBuffImmunity(buffId);
    }
}
