using UnityEngine;

/// <summary>
/// 投射物型技能效果：生成子弹/火球沿方向飞行，支持散射。
/// 右键 -> Create -> Game -> Skill Effect -> Projectile
/// </summary>
[CreateAssetMenu(fileName = "ProjectileEffect", menuName = "Game/Skill Effect/Projectile")]
public class ProjectileSkillEffect : SkillEffectBase
{
    [Header("投射物配置")]
    [Tooltip("投射物预制体（需挂载 Projectile 组件）")]
    public GameObject projectilePrefab;

    [Tooltip("飞行速度")]
    public float speed = 10f;

    [Tooltip("投射物数量（扇形散射）")]
    public int count = 1;

    [Tooltip("散射角度（仅 count > 1 时生效）")]
    [Range(0f, 180f)] public float spreadAngle = 30f;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外投射物数量（0=不成长）")]
    public int countPerLevel = 0;
    [Tooltip("每级额外散射角度（度，0=不成长）")]
    public float spreadAnglePerLevel = 0f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("ProjectileSkillEffect: projectilePrefab 为空！");
            return;
        }

        // 机制成长：投射物数量/散射角随等级提升
        int actualCount = count + (level - 1) * countPerLevel;
        float actualSpread = spreadAngle + (level - 1) * spreadAnglePerLevel;
        actualCount = Mathf.Max(1, actualCount);

        Vector3 spawnPos = caster.CasterTransform.position;
        float baseDamage = caster.GetAttackStrength() * damageMultiplier;

        if (actualCount == 1)
        {
            SpawnOne(spawnPos, direction, baseDamage, speed, ownerType, caster, level);
        }
        else
        {
            float halfSpread = actualSpread * 0.5f;
            float angleStep = actualCount > 1 ? actualSpread / (actualCount - 1) : 0f;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < actualCount; i++)
            {
                float angle = baseAngle - halfSpread + angleStep * i;
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                SpawnOne(spawnPos, dir, baseDamage, speed, ownerType, caster, level);
            }
        }
    }

    private void SpawnOne(Vector3 pos, Vector2 dir, float dmg, float spd,
                                  Projectile.OwnerType owner, ISkillCaster caster, int level)
    {
        GameObject go = Instantiate(projectilePrefab, pos, Quaternion.identity);
        Projectile proj = go.GetComponent<Projectile>();
        if (proj == null) proj = go.AddComponent<Projectile>();
        proj.Initialize(dir, spd, dmg, owner, caster.CasterTransform.gameObject, level);
    }
}
