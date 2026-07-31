using UnityEngine;

/// <summary>
/// 怪物生成器 — 可独立使用或挂载在房间上
/// 负责从配置中读取怪物池并按权重生成敌人
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("生成配置")]
    [Tooltip("引用房间配置 (为空则使用下方手动配置)")]
    public RoomConfig roomConfig;

    [Tooltip("手动配置 — 仅在没有 roomConfig 时使用")]
    public EnemySpawnEntry[] manualEntries;
    public int manualMinEnemies = 1;
    public int manualMaxEnemies = 3;

    [Header("生成点")]
    public Transform[] spawnPoints;

    /// <summary>存活怪物追踪</summary>
    public event System.Action OnAllEnemiesDefeated;

    private int _aliveCount;
    private bool _hasSpawned;

    /// <summary>开始生成</summary>
    public void Spawn()
    {
        if (_hasSpawned) return;
        _hasSpawned = true;

        var entries = roomConfig != null ? roomConfig.enemyPool : 
            (manualEntries != null ? new System.Collections.Generic.List<EnemySpawnEntry>(manualEntries) : null);

        if (entries == null || entries.Count == 0) return;

        int min = roomConfig != null ? roomConfig.minEnemies : manualMinEnemies;
        int max = roomConfig != null ? roomConfig.maxEnemies : manualMaxEnemies;
        int total = Random.Range(min, max + 1);
        int available = spawnPoints != null ? spawnPoints.Length : 0;
        if (available == 0) return;

        int count = Mathf.Min(total, available);

        // 洗牌生成点
        var indices = new int[available];
        for (int i = 0; i < available; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < count; i++)
        {
            var entry = PickWeighted(entries);
            if (entry == null || entry.enemyPrefab == null) continue;

            var point = spawnPoints[indices[i]];
            var enemy = Instantiate(entry.enemyPrefab, point.position, Quaternion.identity, transform);

            _aliveCount++;

            // 监听死亡和同化（使用 IEnemy 接口以便兼容重构后的敌人）
            if (enemy.TryGetComponent<IEnemy>(out var ienemy))
            {
                ienemy.OnDied += () =>
                {
                    _aliveCount--;
                    if (_aliveCount <= 0)
                        OnAllEnemiesDefeated?.Invoke();
                };
                ienemy.OnAssimilated += (_) =>
                {
                    _aliveCount--;
                    if (_aliveCount <= 0)
                        OnAllEnemiesDefeated?.Invoke();
                };
            }
        }
    }

    private EnemySpawnEntry PickWeighted(System.Collections.Generic.List<EnemySpawnEntry> entries)
    {
        float total = 0f;
        foreach (var e in entries) total += e.weight;
        if (total <= 0f) return entries[0];

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var e in entries)
        {
            cumulative += e.weight;
            if (roll <= cumulative) return e;
        }
        return entries[^1];
    }
}
