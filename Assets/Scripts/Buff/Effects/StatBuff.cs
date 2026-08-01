using UnityEngine;

/// <summary>
/// 属性修改 Buff — 修改目标某个属性值。
/// 通过 StatModifier 支持加算/乘算/覆盖叠加。
///
/// 示例: "+30% 攻击力" → statName="AttackStrength", modifier={Multiplicative, 1.3f}
///        "+50 生命值" → statName="MaxHealth", modifier={Additive, 50f}
/// </summary>
[CreateAssetMenu(fileName = "StatBuff", menuName = "Game/Buff Effect/Stat")]
public class StatBuff : BuffEffectBase
{
    [Header("属性修改")]
    [Tooltip("属性名: MaxHealth / MoveSpeed / AttackStrength / BulletSpeed / ShootCooldown / ColliderRadius")]
    public string statName;

    [Tooltip("修改器（加算/乘算/覆盖 + 值）")]
    public StatModifier modifier;

    // 记录修改前的值，用于 OnRemove 恢复
    private float _originalValue;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var stats = target.GetComponent<PlayerStats>();
        if (stats == null)
        {
            Debug.LogWarning($"StatBuff: target '{target.name}' 没有 PlayerStats 组件");
            return;
        }

        _originalValue = stats.GetStatValue(statName);
        float newValue = StatModifier.Apply(_originalValue, modifier);
        stats.SetStatValue(statName, newValue);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        var stats = target.GetComponent<PlayerStats>();
        if (stats == null) return;

        stats.SetStatValue(statName, _originalValue);
    }
}
