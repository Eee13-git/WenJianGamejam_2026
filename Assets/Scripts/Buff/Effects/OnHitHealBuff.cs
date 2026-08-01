using UnityEngine;

/// <summary>
/// 命中治疗 Buff — 挂载者攻击命中时回复自身生命。
/// 适用于 "吸血鬼之触" 类道具效果。
/// </summary>
[CreateAssetMenu(fileName = "OnHitHealBuff", menuName = "Game/Buff Effect/On Hit Heal")]
public class OnHitHealBuff : BuffEffectBase
{
    [Header("治疗")]
    [Tooltip("每次命中治疗量")]
    public float healAmount = 3f;

    [Tooltip("触发概率 (0-1)")]
    [Range(0f, 1f)] public float procChance = 1f;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        Projectile.OnAnyProjectileHit += HandleProjectileHit;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        Projectile.OnAnyProjectileHit -= HandleProjectileHit;
    }

    private void HandleProjectileHit(Projectile projectile, GameObject hitTarget)
    {
        // 检查概率
        if (Random.value > procChance) return;

        // 治疗投射物的主人
        GameObject caster = projectile.Caster;
        if (caster == null) return;
        var healable = caster.GetComponent<IHealable>();
        healable?.Heal(healAmount);
    }
}
