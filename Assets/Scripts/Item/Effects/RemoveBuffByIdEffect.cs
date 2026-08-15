using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移除指定 Buff 的道具效果 — 按 buffId 移除目标身上所有同 ID 的 Buff。
/// 用于阶段道具升级时替换旧阶段效果。
/// OnRemove 时重新挂回被移除的 BuffData（支持降级还原）。
/// </summary>
[CreateAssetMenu(fileName = "RemoveBuffEffect", menuName = "Game/Item Effect/Remove Buff")]
public class RemoveBuffByIdEffect : ItemEffectBase
{
    [Tooltip("要移除的 Buff ID")]
    [SerializeField] private string _buffId;

    [Tooltip("要重新挂载的 BuffData（移除时恢复）。如果为空则不恢复")]
    [SerializeField] private BuffData _restoreBuffData;

    private readonly List<BuffInstance> _removedBuffs = new List<BuffInstance>();

    public override void OnAcquire(GameObject owner)
    {
        var buffManager = owner.GetComponent<BuffManager>();
        if (buffManager == null) return;

        _removedBuffs.Clear();
        var buffs = buffManager.GetBuffs(_buffId);
        if (buffs == null) return;

        foreach (var buff in buffs)
        {
            if (buff == null) continue;
            _removedBuffs.Add(buff);
            buffManager.RemoveBuff(buff);
        }
    }

    public override void OnRemove(GameObject owner)
    {
        var buffManager = owner.GetComponent<BuffManager>();
        if (buffManager == null) return;

        if (_restoreBuffData != null)
        {
            var instance = buffManager.ApplyBuff(_restoreBuffData, owner);
            if (instance != null)
                instance.Indestructible = true;
        }

        _removedBuffs.Clear();
    }
}
