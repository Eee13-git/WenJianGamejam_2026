using System;
using UnityEngine;

/// <summary>
/// 货币管理器 — 挂载在 Player 上，追踪 ATP 数量。
/// 负责货币的增减，通过事件通知 UI。
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    [Header("货币")]
    [SerializeField] private int _atp;

    /// <summary>当前 ATP 数量</summary>
    public int ATP => _atp;

    /// <summary>ATP 变化: (newAmount)</summary>
    public event Action<int> OnATPChanged;

    /// <summary>添加货币</summary>
    public void Add(int amount)
    {
        _atp += amount;
        OnATPChanged?.Invoke(_atp);
    }

    /// <summary>消费货币，返回是否足够</summary>
    public bool Spend(int amount)
    {
        if (_atp < amount) return false;
        _atp -= amount;
        OnATPChanged?.Invoke(_atp);
        return true;
    }
}
