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
    private bool _defeatCheckPending;

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

            RegisterEnemy(enemy);
        }
    }

    /// <summary>
    /// 注册一个敌人到房间计数，并监听其死亡/同化事件。
    /// 生成器直接生成的敌人与亡语（死亡分裂）爆出的敌人都通过此方法计数，
    /// 否则亡语怪不计入计数会导致房间门提前开启。
    /// 同时转发到 RoomManager（真正的门锁控制者）的 _aliveEnemies。
    /// </summary>
    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        _aliveCount++;
        _defeatCheckPending = false;  // 新敌人注册，取消 pending 的开门检查

        // 转发到 RoomManager（门锁由 RoomManager._aliveEnemies 控制）
        var room = GetComponentInParent<RoomManager>();
        if (room != null)
            room.RegisterEnemy(enemy);

        // 监听死亡和同化（使用 IEnemy 接口以便兼容重构后的敌人）
        if (enemy.TryGetComponent<IEnemy>(out var ienemy))
        {
            ienemy.OnDied += () =>
            {
                _aliveCount--;
                TryDefeat();
            };
            ienemy.OnAssimilated += (_) =>
            {
                _aliveCount--;
                TryDefeat();
            };
        }
    }

    /// <summary>
    /// 检查是否所有敌人都已击败。延迟到下一帧执行，
    /// 给亡语（OnDied 事件链里的 Instantiate + RegisterEnemy）留出执行时间。
    /// 若延迟期间有新敌人注册（亡语生成），则取消开门。
    /// </summary>
    private void TryDefeat()
    {
        if (_aliveCount > 0) return;
        if (_defeatCheckPending) return;

        _defeatCheckPending = true;
        StartCoroutine(DeferredDefeatCheck());
    }

    private System.Collections.IEnumerator DeferredDefeatCheck()
    {
        // 等一帧，让同一帧内其他 OnDied 订阅者（亡语 buff）执行完毕
        yield return null;

        // 期间有新敌人注册 → 取消开门（_defeatCheckPending 已被 RegisterEnemy 清 false）
        if (!_defeatCheckPending)
            yield break;

        _defeatCheckPending = false;

        if (_aliveCount <= 0)
            OnAllEnemiesDefeated?.Invoke();
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
