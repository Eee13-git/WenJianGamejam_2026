using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂载 Buff 道具效果 — 获得道具时给持有者挂载一个 Buff。
/// 每次 OnAcquire 创建新 Buff 实例，支持叠加持有。
/// 移除时销毁最后一个 Buff。
/// </summary>
[CreateAssetMenu(fileName = "ApplyBuffEffect", menuName = "Game/Item Effect/Apply Buff")]
public class ApplyBuffEffect : ItemEffectBase
{
    [Header("Buff 配置")]
    [Tooltip("要挂载的 Buff 数据资产")]
    public BuffData buffData;

    /// <summary>已挂载的 Buff 实例列表（用于逐层移除）</summary>
    private readonly List<BuffInstance> _appliedBuffs = new List<BuffInstance>();

    public override void OnAcquire(GameObject owner)
    {
        if (buffData == null)
        {
            Debug.LogWarning("ApplyBuffEffect: buffData 为空！");
            return;
        }

        var buffManager = owner.GetComponent<BuffManager>();
        if (buffManager == null)
        {
            Debug.LogWarning($"ApplyBuffEffect: owner '{owner.name}' 没有 BuffManager 组件");
            return;
        }

        var instance = buffManager.ApplyBuff(buffData, owner);

        // 藏品（道具）来源的 buff 不可被净化类技能清除
        if (instance != null)
            instance.Indestructible = true;

        _appliedBuffs.Add(instance);
    }

    public override void OnRemove(GameObject owner)
    {
        if (_appliedBuffs.Count == 0) return;

        var buffManager = owner.GetComponent<BuffManager>();
        if (buffManager == null) return;

        // 移除最后一个（LIFO）
        int lastIndex = _appliedBuffs.Count - 1;
        var instance = _appliedBuffs[lastIndex];
        _appliedBuffs.RemoveAt(lastIndex);

        buffManager.RemoveBuff(instance);
    }
}
