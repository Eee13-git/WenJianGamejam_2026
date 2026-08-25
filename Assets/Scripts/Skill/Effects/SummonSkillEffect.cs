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

    [Header("召唤上限")]
    [Tooltip("场上同时存活的本技能召唤物上限（0=无限制）。达到上限后停止召唤，等场上减少后再补")]
    [Min(0)] public int maxActiveCount = 0;

    [Header("存在时间上限")]
    [Tooltip("召唤物存在时间上限（秒），到时死亡/销毁。0=无限制（不推荐，所有召唤物都应有限期）")]
    [Min(0)] public float lifetime = 20f;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外召唤数量（0=不成长）")]
    public int countPerLevel = 0;

    /// <summary>本技能召唤且仍存活的单位，按阵营分开追踪（玩家/敌人上限互不相通）</summary>
    private readonly List<GameObject> _playerMinions = new List<GameObject>();
    private readonly List<GameObject> _enemyMinions = new List<GameObject>();

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
    {
        MonoBehaviour mono = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (mono == null) return;
        mono.StartCoroutine(SummonRoutine(caster, ownerType, damageMultiplier, level));
    }

    private System.Collections.IEnumerator SummonRoutine(ISkillCaster caster, Projectile.OwnerType ownerType,
        float damageMultiplier = 1f, int level = 1)
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

        // 机制成长：召唤数量随等级提升
        int actualCount = Mathf.Max(1, count + (level - 1) * countPerLevel);

        Vector2 center = caster.CasterTransform.position;
        Transform parent = caster.CasterTransform.parent;

        for (int i = 0; i < actualCount; i++)
        {
            // 召唤上限（按阵营分别计算，玩家/敌人互不相通）：清理已销毁引用后，若达到上限则停止本次召唤
            var activeMinions = ownerType == Projectile.OwnerType.Player ? _playerMinions : _enemyMinions;
            activeMinions.RemoveAll(m => m == null);
            if (maxActiveCount > 0 && activeMinions.Count >= maxActiveCount)
            {
                Debug.Log($"SummonSkillEffect: {(ownerType == Projectile.OwnerType.Player ? "玩家" : "敌人")}召唤物已达上限 {maxActiveCount}，停止召唤");
                yield break;
            }

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

            // 存在时间上限：所有召唤物统一挂载 SummonLifetime（到时死亡/销毁；被同化为随从自动解除）
            if (lifetime > 0f)
                go.AddComponent<SummonLifetime>().Init(lifetime);

            // 光子光柱：初始化伤害参数（攻击力 + 技能倍率 + 技能等级）
            var beam = go.GetComponent<PhotonBeam>();
            if (beam != null)
                beam.Initialize(ownerType, caster.GetAttackStrength(), damageMultiplier, level);

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

            // 登记到阵营上限追踪（死亡/同化时自动移除）
            TrackMinion(go, ownerType);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>登记召唤物到对应阵营的存活列表，死亡/同化后自动移除</summary>
    private void TrackMinion(GameObject go, Projectile.OwnerType ownerType)
    {
        var activeMinions = ownerType == Projectile.OwnerType.Player ? _playerMinions : _enemyMinions;
        activeMinions.Add(go);
        var core = go.GetComponent<EnemyCore>();
        if (core != null)
        {
            core.OnDied += () => activeMinions.Remove(go);
            core.OnAssimilated += (_) => activeMinions.Remove(go);
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
