using UnityEngine;

/// <summary>
/// 泛用即死 buff 效果 — OnApply 瞬间杀死目标。
/// 用于"释放技能后即死""出生即死"等机制。
/// 配合 ApplyBuffSkillEffect 使用：技能执行时给施法者挂上此 buff → 立即死亡。
/// 右键 -> Create -> Game -> Buff Effect -> Instant Death
/// </summary>
[CreateAssetMenu(fileName = "InstantDeathBuff", menuName = "Game/Buff Effect/Instant Death")]
public class InstantDeathBuff : BuffEffectBase
{
    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(99999f);
    }

    public override void OnRemove(GameObject target, BuffInstance buff) { }
}
