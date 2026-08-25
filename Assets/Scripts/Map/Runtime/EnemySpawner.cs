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

            // 层主题色调 + 难度缩放（与地图环境相配、逐层增强）
            int difficulty = roomConfig != null ? roomConfig.difficultyLevel : 1;
            ApplyLayerTintAndScaling(enemy, difficulty);

            RegisterEnemy(enemy);
        }
    }

    /// <summary>
    /// 按当前层配置对生成敌人做两件事：
    /// 1) 层主题色调：SpriteRenderer 叠加 MapConfig.layerTint（敌人与地图环境相配）
    /// 2) 难度缩放：按 difficultyLevel 提升 HP/伤害/速度（第 1 层=基础值，逐层增强）
    /// </summary>
    private static void ApplyLayerTintAndScaling(GameObject enemy, int difficultyLevel)
    {
        // 层主题色调
        var sr = enemy.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color layerTint = Color.white;
            if (MapManager.Instance != null && MapManager.Instance.MapConfigAsset != null)
                layerTint = MapManager.Instance.MapConfigAsset.layerTint;
            // 保留预制体原有 tint 的相对表现，再叠加层色调（乘法）
            sr.color = sr.color * layerTint;
        }

        // 难度缩放（中后期逐层增强；后期层敌人属性显著高于基础）
        if (difficultyLevel <= 1) return;
        // 前段缓升、后段加速：difficulty 2~4 用线性，5 起额外叠加后期加成
        float t = difficultyLevel - 1;
        float lateBonus = difficultyLevel >= 5 ? (difficultyLevel - 4) * 0.5f : 0f;  // 5层起每层再 +50% 系数
        float hpMul  = 1f + 0.18f * t + lateBonus;     // 每层 +18% HP + 后期加成
        float dmgMul = 1f + 0.12f * t + lateBonus;     // 每层 +12% 伤害 + 后期加成
        float spdMul = 1f + 0.06f * t;                 // 每层 +6% 速度

        var core = enemy.GetComponent<EnemyCore>();
        if (core == null || core.Health == null) return;
        var stats = core.Health;

        stats.SetStatValue("MaxHealth", stats.GetStatValue("MaxHealth") * hpMul);
        stats.SetStatValue("ContactDamage", stats.GetStatValue("ContactDamage") * dmgMul);
        stats.SetStatValue("ChaseSpeed", stats.GetStatValue("ChaseSpeed") * spdMul);
        stats.SetStatValue("PatrolSpeed", stats.GetStatValue("PatrolSpeed") * spdMul);
        // 检测范围也随难度提升（中后期敌人更敏锐）
        stats.SetStatValue("DetectionRange", stats.GetStatValue("DetectionRange") * spdMul);
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
