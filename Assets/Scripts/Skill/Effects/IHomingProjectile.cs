using UnityEngine;

/// <summary>
/// 通用追踪投射物接口 — 由 HomingProjectileSkillEffect 调用。
/// 任何实现此接口的投射物预制体都可被通用追踪技能效果使用。
/// </summary>
public interface IHomingProjectile
{
    /// <summary>初始化追踪投射物：目标、速度、攻击力、伤害系数、总伤害、阵营</summary>
    void Initialize(Transform target, float speed,
        float casterAttackStrength, float damageMultiplier,
        float totalDamage, Projectile.OwnerType ownerType);
}
