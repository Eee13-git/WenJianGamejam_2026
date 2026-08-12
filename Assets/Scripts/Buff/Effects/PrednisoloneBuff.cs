using UnityEngine;

/// <summary>
/// 泼尼松龙 Buff — 生命值降低时获取 ATP（钱）。
/// 订阅 PlayerStats.OnDamaged，按伤害值的倍率获得货币。永久 Buff。
/// </summary>
[CreateAssetMenu(fileName = "PrednisoloneBuff", menuName = "Game/Buff Effect/Prednisolone")]
public class PrednisoloneBuff : BuffEffectBase
{
    [Header("配置")]
    [Tooltip("每点伤害转化为的 ATP 数量")]
    [SerializeField] private float _atpPerDamage = 1f;

    private PlayerStats _stats;
    private CurrencyManager _currency;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _stats = target.GetComponent<PlayerStats>();
        _currency = target.GetComponent<CurrencyManager>();
        if (_stats == null || _currency == null) return;

        _stats.OnDamaged += OnDamaged;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (_stats != null)
            _stats.OnDamaged -= OnDamaged;
    }

    private void OnDamaged(float damage)
    {
        if (_currency == null) return;

        int atp = Mathf.FloorToInt(damage * _atpPerDamage);
        if (atp > 0)
            _currency.Add(atp);
    }
}
