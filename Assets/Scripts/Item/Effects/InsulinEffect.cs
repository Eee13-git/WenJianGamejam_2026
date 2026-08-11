using UnityEngine;

/// <summary>
/// 胰岛素道具效果 — 消耗所有 ATP 增加攻击力，消耗的 ATP 越多增加越多。
/// 攻击力增加 = 消耗ATP × attackPerATP（加算到 AttackStrength）。
/// OnRemove 时不退还（一次性转化）。
/// </summary>
[CreateAssetMenu(fileName = "InsulinEffect", menuName = "Game/Item Effect/Insulin")]
public class InsulinEffect : ItemEffectBase
{
    [Header("转化配置")]
    [Tooltip("每点 ATP 转化的攻击力 (如 0.5 = 消耗100 ATP → +50 攻击)")]
    [SerializeField] private float _attackPerATP = 0.5f;

    public override void OnAcquire(GameObject owner)
    {
        var currency = owner.GetComponent<CurrencyManager>();
        var stats = owner.GetComponent<PlayerStats>();
        if (currency == null || stats == null) return;

        int atp = currency.ATP;
        if (atp <= 0) return;

        // 消耗所有 ATP
        currency.Spend(atp);

        // 增加攻击力
        float bonus = atp * _attackPerATP;
        float current = stats.GetStatValue("AttackStrength");
        stats.SetStatValue("AttackStrength", current + bonus);
    }

    public override void OnRemove(GameObject owner)
    {
        // 一次性转化，移除道具不退还攻击力
    }
}
