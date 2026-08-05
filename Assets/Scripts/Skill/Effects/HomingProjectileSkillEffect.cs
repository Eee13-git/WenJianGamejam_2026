using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用追踪投射物技能效果 — 搜索范围内最近 N 个敌人，各发射一枚追踪投射物。
/// 具体命中逻辑由投射物预制体（实现 IHomingProjectile）自行处理。
/// </summary>
[CreateAssetMenu(fileName = "HomingProjectileEffect", menuName = "Game/Skill Effect/Homing Projectile")]
public class HomingProjectileSkillEffect : SkillEffectBase
{
    [Header("Projectile")]
    [Tooltip("追踪投射物预制体（需实现 IHomingProjectile）")]
    public GameObject homingProjectilePrefab;

    [Tooltip("投射物飞行速度")]
    public float projectileSpeed = 7f;

    [Tooltip("发射数量")]
    public int projectileCount = 3;

    [Tooltip("敌人搜索半径")]
    public float searchRadius = 10f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        if (homingProjectilePrefab == null)
        {
            Debug.LogWarning("HomingProjectileSkillEffect: homingProjectilePrefab is null!");
            return;
        }

        // Cache prefab statically for chain reaction (avoids Unity prefab self-reference serialization issue)
        if (BacteriophageProjectile.S_PhagePrefab == null)
            BacteriophageProjectile.S_PhagePrefab = homingProjectilePrefab;

        Vector3 origin = caster.CasterTransform.position;
        float casterAttack = caster.GetAttackStrength();
        float totalDamage = casterAttack * damageMultiplier;

        var enemies = FindNearestEnemies(origin, searchRadius, projectileCount);

        if (enemies.Count == 0)
        {
            Fire(origin, null, totalDamage, casterAttack, damageMultiplier, ownerType);
            return;
        }

        foreach (var enemy in enemies)
        {
            Fire(origin, enemy, totalDamage, casterAttack, damageMultiplier, ownerType);
        }
    }

    private List<Transform> FindNearestEnemies(Vector3 origin, float radius, int maxCount)
    {
        var hits = Physics2D.OverlapCircleAll(origin, radius);
        var result = new List<(Transform t, float d)>();

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            var ec = hit.GetComponent<EnemyCore>();
            if (ec == null || ec.IsDead) continue;
            float d = Vector3.Distance(origin, hit.transform.position);
            result.Add((hit.transform, d));
        }

        result.Sort((a, b) => a.d.CompareTo(b.d));

        var final = new List<Transform>();
        int count = Mathf.Min(maxCount, result.Count);
        for (int i = 0; i < count; i++)
            final.Add(result[i].t);
        return final;
    }

    private void Fire(Vector3 origin, Transform target, float totalDamage,
        float casterAttackStrength, float damageMultiplier,
        Projectile.OwnerType ownerType)
    {
        GameObject go = Instantiate(homingProjectilePrefab, origin, Quaternion.identity);
        var hp = go.GetComponent<IHomingProjectile>();
        if (hp == null)
        {
            Debug.LogWarning($"HomingProjectileSkillEffect: prefab '{go.name}' does not implement IHomingProjectile!");
            return;
        }
        hp.Initialize(target, projectileSpeed, casterAttackStrength,
            damageMultiplier, totalDamage, ownerType);
    }
}
