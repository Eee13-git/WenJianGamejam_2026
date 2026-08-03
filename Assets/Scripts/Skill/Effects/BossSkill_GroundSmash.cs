using UnityEngine;

/// <summary>
/// Boss 地面重击：以施法者为中心，圆形 AoE 伤害 + 短暂眩晕玩家。
/// </summary>
[CreateAssetMenu(fileName = "GroundSmash", menuName = "Game/Skill Effect/Ground Smash")]
public class BossSkill_GroundSmash : SkillEffectBase
{
    [Header("范围")]
    public float radius = 3.5f;
    public float warningDuration = 0.5f;

    [Header("眩晕")]
    [Tooltip("玩家被击中后无法输入的时间 (秒)")]
    public float stunDuration = 0.5f;

    [Header("特效")]
    public GameObject warningVfx;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        Vector2 center = caster.CasterTransform.position;
        float damage = caster.GetAttackStrength() * damageMultiplier;

        MonoBehaviour mono = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (mono == null) return;

        mono.StartCoroutine(SmashRoutine(caster, center, damage, ownerType));
    }

    private System.Collections.IEnumerator SmashRoutine(ISkillCaster caster, Vector2 center,
                                                          float damage, Projectile.OwnerType ownerType)
    {
        // 预警
        if (warningVfx != null)
            Object.Instantiate(warningVfx, center, Quaternion.identity);

        yield return new WaitForSeconds(warningDuration);

        // 伤害
        string enemyTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag(enemyTag)) continue;
            if (hit.TryGetComponent<IDamageable>(out var d))
                d.TakeDamage(damage);

            // 眩晕玩家
            if (hit.CompareTag("Player") && hit.TryGetComponent<PlayerController>(out var pc))
                pc.StartCoroutine(StunPlayer(pc));
        }
    }

    private System.Collections.IEnumerator StunPlayer(PlayerController pc)
    {
        pc.InputLocked = true;
        yield return new WaitForSeconds(stunDuration);
        pc.InputLocked = false;
    }
}
