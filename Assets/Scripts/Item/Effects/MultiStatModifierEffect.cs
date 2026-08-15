using UnityEngine;

/// <summary>
/// 多属性修改道具效果 — StatModifierEffect 的多属性版本。
/// 每次 OnAcquire 对数组中每个属性执行加算/乘算，OnRemove 反向操作。
/// 天然支持叠加（每次获取/移除都执行正向/反向操作）。
/// </summary>
[CreateAssetMenu(fileName = "MultiStatModifierEffect", menuName = "Game/Item Effect/Multi Stat Modifier")]
public class MultiStatModifierEffect : ItemEffectBase
{
    /// <summary>可选属性名枚举，与 PlayerStats.GetStatValue/SetStatValue 的 switch 分支一一对应</summary>
    public enum StatName
    {
        MaxHealth,
        Health,
        MoveSpeed,
        AttackStrength,
        AttackStrengthMultiplier,
        BulletSpeed,
        ShotsPerMinute,
        EvolutionTendency
    }

    [System.Serializable]
    public struct StatEntry
    {
        public StatName statName;

        [Tooltip("固定加成值（加算模式下每次加减此值）")]
        public float flatBonus;

        [Tooltip("百分比缩放 1.3 = +30%。乘算模式下生效，OnRemove 时/percentBonus 退回")]
        public float percentBonus;

        [Tooltip("true=乘算(percentBonus), false=加算(flatBonus)")]
        public bool isMultiplicative;
    }

    [SerializeField] private StatEntry[] _stats;

    public override void OnAcquire(GameObject owner)
    {
        var stats = owner.GetComponent<PlayerStats>();
        if (stats == null) return;

        foreach (var entry in _stats)
        {
            if (!System.Enum.IsDefined(typeof(StatName), entry.statName)) continue;

            string name = entry.statName.ToString();
            float current = stats.GetStatValue(name);
            float newValue = entry.isMultiplicative
                ? current * entry.percentBonus
                : current + entry.flatBonus;
            stats.SetStatValue(name, newValue);
        }
    }

    public override void OnRemove(GameObject owner)
    {
        var stats = owner.GetComponent<PlayerStats>();
        if (stats == null) return;

        foreach (var entry in _stats)
        {
            if (entry.statName == StatName.Health) continue;
            if (!System.Enum.IsDefined(typeof(StatName), entry.statName)) continue;

            string name = entry.statName.ToString();
            float current = stats.GetStatValue(name);
            float newValue = entry.isMultiplicative
                ? current / entry.percentBonus
                : current - entry.flatBonus;
            stats.SetStatValue(name, newValue);
        }
    }
}
