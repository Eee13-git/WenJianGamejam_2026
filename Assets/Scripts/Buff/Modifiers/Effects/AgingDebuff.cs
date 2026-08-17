using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 衰老 debuff 效果 — OnApply 时降低目标攻击力，OnRemove 恢复。
/// 用实例字典记录每个 BuffInstance 的原始攻击力（避免 static 字段多实例冲突）。
/// 泛用"降攻"类 debuff，可被多个"降低攻击力"机制复用。
/// 右键 -> Create -> Game -> Buff Effect -> Aging
/// </summary>
[CreateAssetMenu(fileName = "AgingDebuff", menuName = "Game/Buff Effect/Aging")]
public class AgingDebuff : BuffEffectBase
{
    [Tooltip("攻击力倍率（0.3 = 攻击力降至 30%）")]
    public float attackMultiplier = 0.3f;

    private static readonly Dictionary<BuffInstance, float> _originalValues
        = new Dictionary<BuffInstance, float>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var stats = target.GetComponent<PlayerStats>();
        if (stats == null) return;

        float original = stats.GetStatValue("AttackStrength");
        _originalValues[buff] = original;
        stats.SetStatValue("AttackStrength", original * attackMultiplier);

        Debug.Log($"[Aging] {target.name} 衰老：攻击力 {original} → {original * attackMultiplier}");
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        var stats = target.GetComponent<PlayerStats>();
        if (stats == null) return;

        if (_originalValues.TryGetValue(buff, out float original))
        {
            stats.SetStatValue("AttackStrength", original);
            _originalValues.Remove(buff);
            Debug.Log("[Aging] 衰老解除，攻击力恢复");
        }
    }
}
