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

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("ProjectileSkillEffect: projectilePrefab 为空！");
            return;
        }

        Vector3 spawnPos = caster.CasterTransform.position;
        float baseDamage = caster.GetAttackStrength() * damageMultiplier;

        if (count == 1)
        {
            SpawnOne(spawnPos, direction, baseDamage, speed, ownerType);
        }
        else
        {
            float halfSpread = spreadAngle * 0.5f;
            float angleStep = count > 1 ? spreadAngle / (count - 1) : 0f;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle - halfSpread + angleStep * i;
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                SpawnOne(spawnPos, dir, baseDamage, speed, ownerType);
            }
        }
    }

    private void SpawnOne(Vector3 pos, Vector2 dir, float dmg, float spd,
                                  Projectile.OwnerType owner)
    {
        GameObject go = Instantiate(projectilePrefab, pos, Quaternion.identity);
        Projectile proj = go.GetComponent<Projectile>();
        if (proj == null) proj = go.AddComponent<Projectile>();
        proj.Initialize(dir, spd, dmg, owner);
    }
}
