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

    /// <summary>累计获得</summary>
    public int TotalEarned { get; private set; }

    /// <summary>累计消费</summary>
    public int TotalSpent { get; private set; }

    /// <summary>ATP 变化: (newAmount)</summary>
    public event Action<int> OnATPChanged;

    /// <summary>获得货币: (earnedAmount)</summary>
    public event Action<int> OnCurrencyEarned;

    /// <summary>消费货币: (spentAmount)</summary>
    public event Action<int> OnCurrencySpent;

    /// <summary>添加货币</summary>
    public void Add(int amount)
    {
        _atp += amount;
        TotalEarned += amount;
        OnATPChanged?.Invoke(_atp);
        OnCurrencyEarned?.Invoke(amount);
    }

    /// <summary>消费货币，返回是否足够</summary>
    public bool Spend(int amount)
    {
        if (_atp < amount) return false;
        _atp -= amount;
        TotalSpent += amount;
        OnATPChanged?.Invoke(_atp);
        OnCurrencySpent?.Invoke(amount);
        return true;
    }
}
