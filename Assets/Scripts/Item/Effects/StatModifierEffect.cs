using UnityEngine;

/// <summary>
/// 属性修改道具效果 — 每次 OnAcquire/OnRemove 直接加减 delta，天然支持叠加。
///
/// 示例:
///   "疾风之靴" — statName="MoveSpeed", flatBonus=2f → 持有3个 = +6速度
///   "力量腰带" — statName="MaxHealth", flatBonus=50f
///   乘算: percentBonus=1.3f → 每次 +30%，OnRemove 时 /1.3 回退
/// </summary>
[CreateAssetMenu(fileName = "StatModifierEffect", menuName = "Game/Item Effect/Stat Modifier")]
public class StatModifierEffect : ItemEffectBase
{
    [Header("属性修改")]
    [Tooltip("属性名: MaxHealth / MoveSpeed / AttackStrength / BulletSpeed / ShotsPerMinute / EvolutionTendency")]
    public string statName;

    [Tooltip("固定加成值（加算模式下每次加减此值）")]
    public float flatBonus;

    [Tooltip("百分比缩放: 1.3 = +30%。乘算模式下生效，OnRemove 时 /percentBonus 回退")]
    public float percentBonus = 1f;

    [Tooltip("true=乘算(percentBonus), false=加算(flatBonus)")]
    public bool isMultiplicative = false;

    public override void OnAcquire(GameObject owner)
    {
        var stats = owner.GetComponent<PlayerStats>();
        if (stats == null) return;

        float current = stats.GetStatValue(statName);

        float newValue;
        if (isMultiplicative)
            newValue = current * percentBonus + flatBonus;
        else
            newValue = current + flatBonus;

        stats.SetStatValue(statName, newValue);
    }

    public override void OnRemove(GameObject owner)
    {
        var stats = owner.GetComponent<PlayerStats>();
        if (stats == null) return;

        float current = stats.GetStatValue(statName);

        float newValue;
        if (isMultiplicative)
            newValue = (current - flatBonus) / percentBonus;
        else
            newValue = current - flatBonus;

        stats.SetStatValue(statName, newValue);
    }
}
