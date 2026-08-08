using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 管理器 — 挂载到需要接收 Buff 的 GameObject 上。
/// 管理所有活跃 Buff 的生命周期（Apply / Tick / Remove）。
///
/// 用法: 挂载到 Player / Enemy Prefab 上，通过 ApplyBuff(BuffData) 添加 Buff。
/// </summary>
public class BuffManager : MonoBehaviour
{
    private readonly List<BuffInstance> _activeBuffs = new List<BuffInstance>();
    private readonly Dictionary<string, List<BuffInstance>> _buffDict = new Dictionary<string, List<BuffInstance>>();

    // 待移除队列 — 避免在迭代中修改集合
    private readonly List<BuffInstance> _pendingRemoval = new List<BuffInstance>();

    /// <summary>当前活跃 Buff 列表（只读）</summary>
    public IReadOnlyList<BuffInstance> ActiveBuffs => _activeBuffs;

    // ========== 事件 ==========

    /// <summary>Buff 被应用: (buff)</summary>
    public event Action<BuffInstance> OnBuffApplied;
    /// <summary>Buff 被移除: (buff)</summary>
    public event Action<BuffInstance> OnBuffRemoved;
    /// <summary>Buff 堆叠变化: (buff)</summary>
    public event Action<BuffInstance> OnBuffStackChanged;

    // ========== 核心方法 ==========

    /// <summary>
    /// 应用 Buff。如果同 ID Buff 已存在且可叠加，则尝试叠加；
    /// 否则创建新 BuffInstance。
    /// </summary>
    public BuffInstance ApplyBuff(BuffData data, GameObject caster = null)
    {
        if (data == null) return null;

        // 检查是否已有同 ID 的 Buff
        if (_buffDict.TryGetValue(data.buffId, out var existingList) && existingList.Count > 0)
        {
            var existing = existingList[0];

            // 可叠加且未满栈：叠加
            if (data.stackable && existing.CurrentStacks < data.maxStacks)
            {
                existing.AddStack();
                OnBuffStackChanged?.Invoke(existing);
                return existing;
            }

            // 不可叠加 / 已满栈：按叠加行为刷新或延长持续时间
            if (!data.isPermanent)
            {
                switch (data.stackBehavior)
                {
                    case StackBehavior.Refresh:
                        existing.RefreshDuration();
                        break;
                    case StackBehavior.ExtendDuration:
                        existing.ExtendDuration(data.duration);
                        break;
                    case StackBehavior.Independent:
                        // Independent 不可叠加时不刷新（保持独立计时）
                        break;
                }
                OnBuffStackChanged?.Invoke(existing);
            }
            return existing;
        }

        var buff = new BuffInstance(data, gameObject, caster);
        _activeBuffs.Add(buff);

        if (!_buffDict.ContainsKey(data.buffId))
            _buffDict[data.buffId] = new List<BuffInstance>();
        _buffDict[data.buffId].Add(buff);

        data.effect?.OnApply(gameObject, buff);
        OnBuffApplied?.Invoke(buff);
        return buff;
    }

    /// <summary>移除指定 Buff 实例</summary>
    public void RemoveBuff(BuffInstance buff)
    {
        if (buff == null || _pendingRemoval.Contains(buff)) return;
        _pendingRemoval.Add(buff);
    }

    /// <summary>移除指定 ID 的所有 Buff</summary>
    public void RemoveBuffById(string buffId)
    {
        if (_buffDict.TryGetValue(buffId, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
                RemoveBuff(list[i]);
        }
    }

    /// <summary>
    /// 清除所有可净化的 Debuff（排除 Indestructible 标记的藏品/道具 debuff）。
    /// 返回被清除的 debuff 数量。
    /// </summary>
    public int RemoveAllDebuffs()
    {
        var toRemove = new List<BuffInstance>();
        foreach (var buff in _activeBuffs)
        {
            if (buff == null) continue;
            if (buff.Data.buffType == BuffType.Debuff && !buff.Indestructible)
                toRemove.Add(buff);
        }

        foreach (var buff in toRemove)
            RemoveBuff(buff);

        return toRemove.Count;
    }

    /// <summary>是否有指定 Buff</summary>
    public bool HasBuff(string buffId) =>
        _buffDict.TryGetValue(buffId, out var list) && list.Count > 0;

    /// <summary>获取指定 ID 的第一个 Buff 实例</summary>
    public BuffInstance GetBuff(string buffId)
    {
        if (_buffDict.TryGetValue(buffId, out var list) && list.Count > 0)
            return list[0];
        return null;
    }

    /// <summary>获取指定 ID 的所有 Buff 实例</summary>
    public IReadOnlyList<BuffInstance> GetBuffs(string buffId)
    {
        if (_buffDict.TryGetValue(buffId, out var list))
            return list;
        return System.Array.Empty<BuffInstance>();
    }

    /// <summary>获取 Buff 堆叠总数（所有同 ID Buff 的堆叠和）</summary>
    public int GetBuffStackCount(string buffId)
    {
        int total = 0;
        if (_buffDict.TryGetValue(buffId, out var list))
        {
            foreach (var b in list)
                total += b.CurrentStacks;
        }
        return total;
    }

    // ========== Unity 生命周期 ==========

    private void Update()
    {
        float dt = Time.deltaTime;

        // Tick 所有活跃 Buff
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = _activeBuffs[i];
            if (buff.IsActive)
                buff.Tick(dt);
        }

        // 处理过期 Buff
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            if (!_activeBuffs[i].IsActive || _activeBuffs[i].IsExpired)
                _pendingRemoval.Add(_activeBuffs[i]);
        }

        // 执行待移除
        ProcessRemovalQueue();
    }

    private void ProcessRemovalQueue()
    {
        for (int i = 0; i < _pendingRemoval.Count; i++)
        {
            var buff = _pendingRemoval[i];
            if (buff == null) continue;

            buff.IsActive = false;
            buff.Data.effect?.OnRemove(gameObject, buff);
            _activeBuffs.Remove(buff);

            if (_buffDict.TryGetValue(buff.Data.buffId, out var list))
            {
                list.Remove(buff);
                if (list.Count == 0)
                    _buffDict.Remove(buff.Data.buffId);
            }

            OnBuffRemoved?.Invoke(buff);
        }
        _pendingRemoval.Clear();
    }

    private void OnDestroy()
    {
        // 清理所有 Buff
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = _activeBuffs[i];
            buff.Data.effect?.OnRemove(gameObject, buff);
        }
        _activeBuffs.Clear();
        _buffDict.Clear();
    }
}
