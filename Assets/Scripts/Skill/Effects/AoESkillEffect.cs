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

    [Header("预警（可选）")]
    [Tooltip("预警时间（秒），>0 则延迟生效并播放预警特效")]
    public float warningDuration;

    [Tooltip("预警特效（可选，须配合 warningDuration 使用）")]
    public GameObject warningVfx;

    [Header("眩晕（可选）")]
    [Tooltip("玩家被击中后眩晕时间（秒），0=不眩晕")]
    public float stunDuration;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外伤害半径（0=不成长）")]
    public float radiusPerLevel = 0f;
    [Tooltip("每级额外眩晕时长（秒，0=不成长）")]
    public float stunDurationPerLevel = 0f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
    {
        // 机制成长：半径/眩晕时长随等级提升
        float actualRadius = radius + (level - 1) * radiusPerLevel;
        float actualStun = stunDuration + (level - 1) * stunDurationPerLevel;

        Vector2 center = centeredOnCaster
            ? (Vector2)caster.CasterTransform.position
            : (Vector2)caster.CasterTransform.position + direction * 2f;

        float damage = caster.GetAttackStrength() * damageMultiplier;

        MonoBehaviour mono = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (mono != null)
            mono.StartCoroutine(DelayedExecute(center, damage, ownerType, actualRadius, actualStun));
        else
            ApplyDamage(center, damage, ownerType, actualRadius, actualStun);
    }

    private System.Collections.IEnumerator DelayedExecute(Vector2 center,
        float damage, Projectile.OwnerType ownerType, float actualRadius, float actualStun)
    {
        if (warningVfx != null)
            Object.Instantiate(warningVfx, center, Quaternion.identity);

        if (warningDuration > 0f)
            yield return new WaitForSeconds(warningDuration);

        ApplyDamage(center, damage, ownerType, actualRadius, actualStun);
    }

    private void ApplyDamage(Vector2 center, float damage, Projectile.OwnerType ownerType,
        float actualRadius, float actualStun)
    {
        if (impactVfx != null)
            Object.Instantiate(impactVfx, center, Quaternion.identity);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, actualRadius);
        string enemyTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";

        foreach (var hit in hits)
        {
            if (!hit.CompareTag(enemyTag)) continue;
            if (hit.TryGetComponent<IDamageable>(out var d))
                d.TakeDamage(damage);

            if (actualStun > 0f && hit.CompareTag("Player")
                && hit.TryGetComponent<PlayerController>(out var pc))
                pc.StartCoroutine(StunRoutine(pc, actualStun));
        }
    }

    private static System.Collections.IEnumerator StunRoutine(PlayerController pc, float duration)
    {
        // 硬直免疫（镇痛阻滞等 buff 期间不受眩晕）
        if (pc.IgnoreStun) yield break;

        pc.InputLocked = true;
        yield return new WaitForSeconds(duration);
        pc.InputLocked = false;
    }
}
