using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 消耗血量召唤 buff 效果 — 周期性消耗自身血量并召唤随从。
/// 血量越低，召唤间隔越短（召唤越快）。
/// 召唤逻辑复用 SummonSkillEffect 的预制体池 + 阵营标记。
/// 泛用类：消耗量、基础间隔、召唤数量、预制体池均可配置。
/// 右键 -> Create -> Game -> Buff Effect -> Blood Cost Summon
/// </summary>
[CreateAssetMenu(fileName = "BloodCostSummonBuff", menuName = "Game/Buff Effect/Blood Cost Summon")]
public class BloodCostSummonBuff : BuffEffectBase
{
    [Header("血量消耗")]
    [Tooltip("每次召唤消耗的自身血量")]
    public float healthCost = 5f;

    [Header("召唤配置")]
    [Tooltip("敌方阵营召唤的随从预制体池")]
    public GameObject[] enemyMinionPrefabs;
    [Tooltip("友方阵营召唤的随从预制体池")]
    public GameObject[] playerMinionPrefabs;
    [Tooltip("每次召唤数量")]
    public int count = 3;
    [Tooltip("召唤散布半径")]
    public float spawnRadius = 2f;

    [Header("召唤节奏")]
    [Tooltip("血量满时的召唤间隔（秒）")]
    public float maxInterval = 6f;
    [Tooltip("血量最低时的召唤间隔（秒）")]
    public float minInterval = 2f;

    private static readonly Dictionary<BuffInstance, float> _timers = new Dictionary<BuffInstance, float>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _timers[buff] = 0f;
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime)
    {
        if (!_timers.TryGetValue(buff, out float timer))
        {
            timer = 0f;
            _timers[buff] = timer;
        }

        // 根据当前血量百分比计算召唤间隔（血量越低越快）
        float currentInterval = GetCurrentInterval(target);

        timer += deltaTime;
        if (timer < currentInterval)
        {
            _timers[buff] = timer;
            return;
        }

        _timers[buff] = 0f;

        // 消耗自身血量
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(healthCost);

        // 召唤随从
        SummonMinions(target);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        _timers.Remove(buff);
    }

    private float GetCurrentInterval(GameObject target)
    {
        // 尝试读取血量百分比
        float hpPercent = 1f;
        var stats = target.GetComponent<EnemyStats>();
        if (stats != null && stats.MaxHealth > 0f)
            hpPercent = stats.CurrentHealth / stats.MaxHealth;
        else
        {
            var playerStats = target.GetComponent<PlayerStats>();
            if (playerStats != null && playerStats.MaxHealth > 0f)
                hpPercent = playerStats.CurrentHealth / playerStats.MaxHealth;
        }

        // hpPercent=1 → maxInterval, hpPercent=0 → minInterval
        return Mathf.Lerp(minInterval, maxInterval, hpPercent);
    }

    private void SummonMinions(GameObject caster)
    {
        bool isPlayerSide = caster.CompareTag("Player");
        var pool = isPlayerSide ? playerMinionPrefabs : enemyMinionPrefabs;
        if (pool == null || pool.Length == 0) return;

        string selfTag = isPlayerSide ? "Player" : "Enemy";
        Vector2 center = caster.transform.position;
        Transform parent = caster.transform.parent;

        // 找到所属房间（门锁由 RoomManager._aliveEnemies 控制）+ 生成器（计数）
        var room = parent != null ? parent.GetComponentInParent<RoomManager>() : null;
        var spawner = parent != null ? parent.GetComponentInParent<EnemySpawner>() : null;

        for (int i = 0; i < count; i++)
        {
            var prefab = pool[Random.Range(0, pool.Length)];
            if (prefab == null) continue;

            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            var go = Object.Instantiate(prefab, center + offset, Quaternion.identity, parent);
            go.name = $"Summon_{prefab.name}_{i}";
            go.tag = selfTag;

            // 设置初始目标
            var core = go.GetComponent<EnemyCore>();
            if (core != null)
            {
                // 找最近敌方
                string targetTag = isPlayerSide ? "Enemy" : "Player";
                var targets = GameObject.FindGameObjectsWithTag(targetTag);
                if (targets.Length > 0)
                {
                    Transform best = null;
                    float bestDist = float.MaxValue;
                    foreach (var t in targets)
                    {
                        float d = Vector2.SqrMagnitude((Vector2)t.transform.position - center);
                        if (d < bestDist) { bestDist = d; best = t.transform; }
                    }
                    if (best != null) core.PlayerTarget = best;
                }
            }

            // 计入房间敌人计数，避免房间门提前开启
            if (!isPlayerSide)
            {
                room?.RegisterEnemy(go);
                spawner?.RegisterEnemy(go);
            }
        }

        Debug.Log($"[BloodCostSummon] {caster.name} 消耗 {healthCost} 血 → 召唤 {count} 个随从");
    }
}
