using UnityEngine;

/// <summary>
/// 范围伤害技能效果：以施法者或目标点为中心造成圆形范围伤害。
/// 右键 -> Create -> Game -> Skill Effect -> AoE Damage
/// </summary>
[CreateAssetMenu(fileName = "AoEEffect", menuName = "Game/Skill Effect/AoE Damage")]
public class AoESkillEffect : SkillEffectBase
{
    [Header("范围配置")]
    [Tooltip("伤害半径")]
    public float radius = 3f;

    [Tooltip("是否以施法者为中心（否则以施法方向前方2单位处为中心）")]
    public bool centeredOnCaster = true;

    [Tooltip("打击特效预制体（可选）")]
    public GameObject impactVfx;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        Vector2 center = centeredOnCaster
            ? (Vector2)caster.CasterTransform.position
            : (Vector2)caster.CasterTransform.position + direction * 2f;

        float damage = caster.GetAttackStrength() * damageMultiplier;

        if (impactVfx != null)
            Instantiate(impactVfx, center, Quaternion.identity);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        string enemyTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";

        foreach (var hit in hits)
        {
            if (!hit.CompareTag(enemyTag)) continue;
            if (hit.TryGetComponent<IDamageable>(out var d))
                d.TakeDamage(damage);
        }
    }
}
