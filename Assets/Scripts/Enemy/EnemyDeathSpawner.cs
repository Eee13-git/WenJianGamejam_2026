using UnityEngine;

/// <summary>
/// 敌人死亡生成器 — 全局自动注册，监听所有敌人死亡事件。
/// 根据配置（EnemyDeathSpawnConfig）在死亡位置按概率生成额外单位（如噬菌体、分裂怪等）。
/// 支持多组生成条目，每组可指定概率、排除列表、是否排除 Boss。
/// 配置通过 Resources/EnemyDeathSpawnConfig.asset 加载。
/// </summary>
public class EnemyDeathSpawner : MonoBehaviour
{
    private static EnemyDeathSpawner _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (_instance == null)
        {
            var go = new GameObject(nameof(EnemyDeathSpawner));
            _instance = go.AddComponent<EnemyDeathSpawner>();
            DontDestroyOnLoad(go);
        }
    }

    private EnemyDeathSpawnConfig _config;

    private void OnEnable()
    {
        EnemyCore.OnAnyEnemyDied += OnEnemyDied;

        _config = Resources.Load<EnemyDeathSpawnConfig>("EnemyDeathSpawnConfig");
        if (_config == null)
            Debug.LogWarning("[EnemyDeathSpawner] 未找到 EnemyDeathSpawnConfig.asset，死亡生成功能禁用");
    }

    private void OnDisable()
    {
        EnemyCore.OnAnyEnemyDied -= OnEnemyDied;
    }

    private void OnEnemyDied(EnemyCore enemy)
    {
        if (_config == null || _config.entries == null) return;

        string enemyName = enemy.config != null ? enemy.config.displayName : "";
        bool isBoss = enemy.GetComponent<BossCore>() != null;

        foreach (var entry in _config.entries)
        {
            if (entry == null || entry.prefab == null) continue;

            // 排除 Boss
            if (entry.excludeBoss && isBoss) continue;

            // 排除指定名称（防自身繁殖等）
            if (entry.excludeNames != null && entry.excludeNames.Contains(enemyName)) continue;

            // 概率判定
            if (Random.value > entry.spawnChance) continue;

            // 生成位置（带散布偏移）
            Vector2 pos = enemy.transform.position;
            if (entry.spawnRadius > 0f)
                pos += Random.insideUnitCircle * entry.spawnRadius;

            var go = Instantiate(entry.prefab, pos, Quaternion.identity);
            go.name = $"DeathSpawn_{entry.prefab.name}";

            // 计入房间敌人计数：亡语生成的噬菌体/分裂怪必须计入 _aliveEnemies，
            // 否则房间门提前开启（且其死亡也不触发清房判定）
            var room = enemy.transform.parent != null
                ? enemy.transform.parent.GetComponentInParent<RoomManager>()
                : null;
            // fallback：按死亡位置匹配房间（敌人无 parent 时，如编辑器测试实例）
            if (room == null)
            {
                foreach (var rr in Object.FindObjectsOfType<RoomRoot>(true))
                {
                    Vector2 half = rr.roomSize * 0.5f;
                    Vector2 p = enemy.transform.position;
                    if (Mathf.Abs(p.x - rr.transform.position.x) <= half.x
                        && Mathf.Abs(p.y - rr.transform.position.y) <= half.y)
                    {
                        room = rr.GetComponent<RoomManager>();
                        break;
                    }
                }
            }
            room?.RegisterEnemy(go);

            Debug.Log($"[EnemyDeathSpawner] {enemyName} 死亡 → 生成 {entry.prefab.name}");
        }
    }
}
