using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召唤技能：在施法者周围随机位置生成单位。
/// 根据施法者阵营自动选择预制体池、Tag 与目标。
/// </summary>
[CreateAssetMenu(fileName = "SummonEffect", menuName = "Game/Skill Effect/Summon")]
public class SummonSkillEffect : SkillEffectBase
{
    [Header("召唤 — 敌方单位池（Boss/Enemy 释放时使用）")]
    [Tooltip("生成的单位 Tag=Enemy，攻击玩家")]
    public GameObject[] enemyMinionPrefabs;

    [Header("召唤 — 友方单位池（Player 释放时使用）")]
    [Tooltip("生成的单位 Tag=Player，攻击敌人")]
    public GameObject[] playerMinionPrefabs;

    [Header("参数")]
    [Tooltip("召唤数量")]
    public int count = 3;

    [Tooltip("生成半径 (围绕施法者)")]
    public float spawnRadius = 4f;

    [Tooltip("生成间隔 (秒)")]
    public float spawnInterval = 0.3f;

    [Header("敌方优先生成（可选）")]
    [Tooltip("生成位置极高概率落在敌方单位坐标（如光子光柱）")]
    public bool preferEnemySpawn = false;
    [Tooltip("落在敌方坐标的概率（0~1，默认 0.9）")]
    [Range(0f, 1f)] public float enemySpawnChance = 0.9f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        MonoBehaviour mono = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (mono == null) return;
        mono.StartCoroutine(SummonRoutine(caster, ownerType));
    }

    private System.Collections.IEnumerator SummonRoutine(ISkillCaster caster, Projectile.OwnerType ownerType)
    {
        GameObject[] pool = ownerType == Projectile.OwnerType.Player
            ? playerMinionPrefabs
            : enemyMinionPrefabs;

        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning("SummonSkillEffect: 无可用预制体池");
            yield break;
        }

        string targetTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        string selfTag   = ownerType == Projectile.OwnerType.Player ? "Player" : "Enemy";

        Vector2 center = caster.CasterTransform.position;
        Transform parent = caster.CasterTransform.parent;

        for (int i = 0; i < count; i++)
        {
            // 生成位置：高优先级判定 → 极高概率在敌方单位坐标
            Vector2 spawnPos;
            if (preferEnemySpawn && Random.value < enemySpawnChance)
            {
                var enemyPos = PickRandomEnemyPosition(targetTag);
                spawnPos = enemyPos.HasValue
                    ? enemyPos.Value
                    : center + Random.insideUnitCircle * spawnRadius;   // 无敌方 → 随机
            }
            else
            {
                spawnPos = center + Random.insideUnitCircle * spawnRadius;
            }

            GameObject prefab = pool[Random.Range(0, pool.Length)];
            if (prefab == null) continue;

            var go = Object.Instantiate(prefab, spawnPos, Quaternion.identity, parent);
            go.name = $"Summon_{ownerType}_{i}_{prefab.name}";

            // 阵营标记
            go.tag = selfTag;

            // 光子光柱：初始化伤害参数
            var beam = go.GetComponent<PhotonBeam>();
            if (beam != null)
                beam.Initialize(ownerType, caster.GetAttackStrength());

            // 设置敌人 Core 的初始目标
            var core = go.GetComponent<EnemyCore>();
            if (core != null)
            {
                var target = FindNearestTarget(spawnPos, targetTag);
                if (target != null)
                    core.PlayerTarget = target; // 字段名是 PlayerTarget，但实际是"攻击目标"
            }

            // 计入房间敌人计数（Boss 召唤的小怪也必须计入，否则房间门提前开启）
            var room = caster.CasterTransform.parent != null
                ? caster.CasterTransform.parent.GetComponentInParent<RoomManager>()
                : null;
            room?.RegisterEnemy(go);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>随机选一个敌方单位坐标（高优先级生成点），无敌方返回 null</summary>
    private static Vector2? PickRandomEnemyPosition(string targetTag)
    {
        var gos = GameObject.FindGameObjectsWithTag(targetTag);
        if (gos == null || gos.Length == 0)
            return null;

        var go = gos[Random.Range(0, gos.Length)];
        return (Vector2)go.transform.position;
    }

    private static Transform FindNearestTarget(Vector2 origin, string targetTag)
    {
        var gos = GameObject.FindGameObjectsWithTag(targetTag);
        if (gos == null || gos.Length == 0) return null;

        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var go in gos)
        {
            float d = Vector2.SqrMagnitude((Vector2)go.transform.position - origin);
            if (d < bestDist)
            {
                bestDist = d;
                best = go.transform;
            }
        }
        return best;
    }
}
