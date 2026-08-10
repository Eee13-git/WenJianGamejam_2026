using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 命中触发 Buff — 挂载者（玩家）的攻击命中目标时触发效果。
/// 适用于 "攻击时给敌人挂 Debuff" 或 "攻击时造成额外伤害"。
///
/// 示例:
///   "毒蛇之牙" — onHitBuff=中毒Debuff数据, procChance=1f
///   "火焰之触" — onHitDamage=10f（额外火焰伤害）
/// </summary>
[CreateAssetMenu(fileName = "OnHitBuff", menuName = "Game/Buff Effect/On Hit")]
public class OnHitBuff : BuffEffectBase
{
    [Header("命中效果")]
    [Tooltip("命中时给目标挂载的 Buff（如中毒、减速）")]
    public BuffData onHitBuff;

    [Tooltip("命中时额外伤害")]
    public float onHitDamage;

    [Tooltip("触发概率 (0-1)，1 = 必定触发")]
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
        // 检测概率
        if (Random.value > procChance)
            return;

        // 额外伤害
        if (onHitDamage > 0f)
        {
            var damageable = hitTarget.GetComponent<IDamageable>();
            damageable?.TakeDamage(onHitDamage);
        }

        // 挂载额外 Buff
        if (onHitBuff != null)
        {
            var targetBuffManager = hitTarget.GetComponent<BuffManager>();
            if (targetBuffManager != null)
            {
                targetBuffManager.ApplyBuff(onHitBuff, projectile.gameObject);
            }
        }
    }
}
