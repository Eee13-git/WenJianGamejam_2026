using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单房间管理器 — 管理单个房间的运行时状态。
/// 负责怪物生成、门锁定/解锁、道具生成等。
/// </summary>
public class RoomManager : MonoBehaviour
{
    [Header("组件引用")]
    public RoomRoot roomRoot;

    [Header("房间状态")]
    [SerializeField] private bool _isCleared;
    private bool _isFirstEnter = true;

    /// <summary>房间是否已清完怪物</summary>
    public bool IsCleared => _isCleared;

    /// <summary>是否首次进入</summary>
    public bool IsFirstEnter => _isFirstEnter;

    /// <summary>当前存活怪物列表</summary>
    private List<GameObject> _aliveEnemies = new();

    /// <summary>所有门 Portal 引用</summary>
    private List<RoomPortal> _portals = new();

    public event System.Action OnRoomCleared;

    private void Awake()
    {
        if (roomRoot == null)
            roomRoot = GetComponent<RoomRoot>();

        _portals.AddRange(GetComponentsInChildren<RoomPortal>());

        // 确保每次实例化都是首次进入状态 (防止序列化残留)
        _isFirstEnter = true;
        _isCleared = false;
    }

    /// <summary>玩家进入房间时调用</summary>
    public void OnPlayerEnter()
    {
        if (roomRoot == null) roomRoot = GetComponent<RoomRoot>();
        if (roomRoot == null || roomRoot.config == null) return;

        Debug.Log($"[RoomManager] Room{roomRoot.roomId} OnPlayerEnter: _isFirstEnter={_isFirstEnter}, enemyPool={roomRoot.config.enemyPool?.Count}, minEnemies={roomRoot.config.minEnemies}");

        if (_isFirstEnter)
        {
            _isFirstEnter = false;
            SpawnEnemies();
            Debug.Log($"[RoomManager] Room{roomRoot.roomId} spawned enemies, _aliveEnemies.Count={_aliveEnemies.Count}");

            if (_aliveEnemies.Count > 0)
            {
                Debug.Log($"[RoomManager] Room{roomRoot.roomId} calling LockDoors...");
                LockDoors();
                Debug.Log($"[RoomManager] Room{roomRoot.roomId} LockDoors done.");
            }
        }
    }

    /// <summary>玩家离开房间时调用</summary>
    public void OnPlayerExit()
    {
        // 暂不需要特殊处理
    }

    /// <summary>锁定所有门 (启用阻挡物 + 禁用触发器)</summary>
    private void LockDoors()
    {
        foreach (var portal in _portals)
        {
            if (portal != null)
                portal.SetLocked(true);
        }
    }

    /// <summary>解锁所有门 (启用触发器 + 禁用阻挡物)</summary>
    private void UnlockDoors()
    {
        foreach (var portal in _portals)
        {
            if (portal != null)
                portal.SetLocked(false);
        }
    }

    /// <summary>生成怪物</summary>
    private void SpawnEnemies()
    {
        if (roomRoot == null || roomRoot.config == null) return;
        
        var cfg = roomRoot.config;
        if (cfg.enemyPool == null || cfg.enemyPool.Count == 0) return;

        // 按权重选择敌人
        int totalEnemies = Random.Range(cfg.minEnemies, cfg.maxEnemies + 1);
        int spawnPointCount = roomRoot.enemySpawnPoints != null ? roomRoot.enemySpawnPoints.Length : 0;

        if (spawnPointCount == 0)
        {
            Debug.LogWarning($"Room {roomRoot.roomId}: 没有敌人生成点");
            return;
        }

        // 随机选取生成点 (不重复)
        var availablePoints = new List<int>();
        for (int i = 0; i < spawnPointCount; i++)
            availablePoints.Add(i);
        Shuffle(availablePoints);

        int spawnCount = Mathf.Min(totalEnemies, availablePoints.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            // 按权重随机选择敌人类型
            var entry = PickWeighted(cfg.enemyPool);
            if (entry == null || entry.enemyPrefab == null) continue;

            var spawnPoint = roomRoot.enemySpawnPoints[availablePoints[i]];
            var enemy = Instantiate(entry.enemyPrefab, spawnPoint.position, Quaternion.identity, transform);
            _aliveEnemies.Add(enemy);

            // 监听敌人死亡
            var damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // 简单方式：注册死亡回调
                if (enemy.TryGetComponent<BaseEnemy>(out var baseEnemy))
                {
                    baseEnemy.OnEnemyDied += () => OnEnemyDied(enemy);
                }
                else
                {
                    // 通用方式：延迟检查
                    StartCoroutine(WatchEnemyDeath(enemy));
                }
            }
        }

        Debug.Log($"Room {roomRoot.roomId}: 生成了 {spawnCount} 个敌人");
    }

    /// <summary>按权重随机选择</summary>
    private EnemySpawnEntry PickWeighted(List<EnemySpawnEntry> entries)
    {
        float totalWeight = 0f;
        foreach (var e in entries) totalWeight += e.weight;
        
        if (totalWeight <= 0f) return entries.Count > 0 ? entries[0] : null;
        
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var e in entries)
        {
            cumulative += e.weight;
            if (roll <= cumulative)
                return e;
        }
        return entries[^1];
    }

    /// <summary>敌人死亡回调</summary>
    private void OnEnemyDied(GameObject enemy)
    {
        _aliveEnemies.Remove(enemy);

        if (_aliveEnemies.Count == 0)
        {
            OnAllEnemiesDefeated();
        }
    }

    /// <summary>所有敌人被击败</summary>
    private void OnAllEnemiesDefeated()
    {
        _isCleared = true;
        
        // 解锁门
        UnlockDoors();

        // 生成道具奖励
        SpawnItems();

        Debug.Log($"Room {roomRoot.roomId}: 已清空!");
        OnRoomCleared?.Invoke();
    }

    /// <summary>生成道具</summary>
    private void SpawnItems()
    {
        if (roomRoot == null || roomRoot.config == null) return;
        var cfg = roomRoot.config;
        if (cfg.itemPool == null || cfg.itemPool.Count == 0) return;

        int totalItems = Random.Range(cfg.minItems, cfg.maxItems + 1);
        int spawnPointCount = roomRoot.itemSpawnPoints != null ? roomRoot.itemSpawnPoints.Length : 0;
        if (spawnPointCount == 0) return;

        int spawnCount = Mathf.Min(totalItems, spawnPointCount);

        for (int i = 0; i < spawnCount; i++)
        {
            int idx = Random.Range(0, cfg.itemPool.Count);
            var itemPrefab = cfg.itemPool[idx];
            if (itemPrefab == null) continue;

            Instantiate(itemPrefab, roomRoot.itemSpawnPoints[i].position, Quaternion.identity, transform);
        }
    }

    /// <summary>协程: 轮询检查敌人死亡 (用于未继承 BaseEnemy 的敌人)</summary>
    private System.Collections.IEnumerator WatchEnemyDeath(GameObject enemy)
    {
        while (enemy != null)
        {
            yield return new WaitForSeconds(0.5f);
        }
        OnEnemyDied(null);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
