using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 单房间管理器 — 管理单个房间的运行时状态。
/// 负责怪物生成、门锁定/解锁、道具生成等。
/// </summary>
public class RoomManager : MonoBehaviour
{
    [Header("组件引用")]
    public RoomRoot roomRoot;

    [Header("货币掉落")]
    [SerializeField] private int _currencyPerEnemy = 3;
    [SerializeField] private Sprite _currencyIcon;

    [Header("房间状态")]
    [SerializeField] private bool _isCleared;
    private bool _isFirstEnter = true;

    [Header("出口生成避让")]
    [Tooltip("玩家与房间中心距离小于此值时，延迟生成下一层出口（0=关闭）")]
    [SerializeField] private float _exitBlockRadius = 1.5f;

    /// <summary>房间是否已清完怪物</summary>
    public bool IsCleared => _isCleared;

    /// <summary>是否首次进入</summary>
    public bool IsFirstEnter => _isFirstEnter;

    /// <summary>获取指定位置的寻路网格（供敌人依赖注入）</summary>
    public PathfindingGrid GetGridAtPosition(Vector2 position)
    {
        if (roomRoot != null && roomRoot.PathGrid != null)
        {
            if (roomRoot.Bounds.Contains(position))
                return roomRoot.PathGrid;
        }
        return null;
    }

    /// <summary>当前存活怪物列表</summary>
    private List<GameObject> _aliveEnemies = new();

    /// <summary>所有门 Portal 引用</summary>
    private List<RoomPortal> _portals = new();

    public event System.Action OnRoomCleared;

    private float _nextGhostCleanupTime;
    private const float GhostCleanupInterval = 2f;

    private void Awake()
    {
        if (roomRoot == null)
            roomRoot = GetComponent<RoomRoot>();

        // 自动加载货币图标
        if (_currencyIcon == null)
            _currencyIcon = Resources.Load<Sprite>("TestAssets/Icons/icon_atp");

        _portals.AddRange(GetComponentsInChildren<RoomPortal>());

        // 确保每次实例化都是首次进入状态 (防止序列化残留)
        _isFirstEnter = true;
        _isCleared = roomRoot.config.maxEnemies == 0? true : false;
    }

    private void Update()
    {
        if (_isCleared) return;
        if (Time.time >= _nextGhostCleanupTime)
        {
            _nextGhostCleanupTime = Time.time + GhostCleanupInterval;
            CleanupGhostEntries();
        }
    }

    /// <summary>
    /// 清理 _aliveEnemies 中的幽灵条目（被销毁但未触发事件的敌人）
    /// 和已死亡/已同化但仍残留在列表中的敌人。
    /// </summary>
    private void CleanupGhostEntries()
    {
        bool removed = false;
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (_aliveEnemies[i] == null)
            {
                _aliveEnemies.RemoveAt(i);
                removed = true;
                continue;
            }
            var core = _aliveEnemies[i].GetComponent<EnemyCore>();
            if (core != null && (core.IsDead || core.IsAssimilated))
            {
                _aliveEnemies.RemoveAt(i);
                removed = true;
            }
        }
        if (removed && _aliveEnemies.Count == 0 && !_isCleared)
        {
            OnAllEnemiesDefeated();
        }
    }

    /// <summary>玩家进入房间时调用</summary>
    public void OnPlayerEnter()
    {
        if (roomRoot == null) roomRoot = GetComponent<RoomRoot>();
        if (roomRoot == null || roomRoot.config == null) return;

        // 新手引导：按房间类型展示上下文提示（首次进入）
        ShowTutorialTipsForRoom();

        // BGM 切换：Boss 房间 → Boss 战音乐；其他房间 → 日常战斗音乐
        if (BgmManager.Instance != null)
        {
            if (roomRoot.config.roomType == RoomType.Boss)
                BgmManager.Instance.PlayBoss();
            else
                BgmManager.Instance.PlayCombat();
        }

        // Shop 房间特殊处理：开张商店
        if (roomRoot.config.roomType == RoomType.Shop)
        {
            if (_isFirstEnter)
            {
                _isFirstEnter = false;
                OpenShopRoom();
            }
            return;
        }

        Debug.Log($"[RoomManager] Room{roomRoot.roomId} OnPlayerEnter: _isFirstEnter={_isFirstEnter}, enemyPool={roomRoot.config.enemyPool?.Count}, minEnemies={roomRoot.config.minEnemies}");

        // 玩家进入时恢复刺陷阱（如果不是已清理状态）
        if (!_isCleared)
            SetSpikesPaused(false);

        if (_isFirstEnter)
        {
            _isFirstEnter = false;

            // 统计：首次进入房间
            GameStatistics.Instance?.RecordRoomVisited(roomRoot.config.roomType);

            // 构建寻路网格（在敌人生成前）
            roomRoot.BuildPathfindingGrid();

            SpawnEnemies();
            Debug.Log($"[RoomManager] Room{roomRoot.roomId} spawned enemies, _aliveEnemies.Count={_aliveEnemies.Count}");

            if (_aliveEnemies.Count > 0)
            {
                Debug.Log($"[RoomManager] Room{roomRoot.roomId} calling LockDoors...");
                LockDoors();
                Debug.Log($"[RoomManager] Room{roomRoot.roomId} LockDoors done.");
            }
            else
            {
                // 无怪物的房间（如 Start 房）：直接生成道具（不生成血量回复物）
                var availablePoints = new List<Transform>();
                if (roomRoot.itemSpawnPoints != null)
                    availablePoints.AddRange(roomRoot.itemSpawnPoints);
                SpawnItems(availablePoints);
            }
        }
    }

    /// <summary>玩家离开房间时调用</summary>
    public void OnPlayerExit()
    {
        // 道具现在是普通 ItemPickup，离开后保留在地图中
    }

    /// <summary>
    /// 新手引导：按房间类型展示上下文提示（首次进入才显示）。
    /// Start 房（无敌人）→ 基础操作；Normal 房 → 战斗规则；Boss 房 → Boss 提示；Shop 房由 ShopManager 负责。
    /// </summary>
    private void ShowTutorialTipsForRoom()
    {
        if (TutorialManager.Instance == null) return;
        var roomType = roomRoot.config.roomType;

        switch (roomType)
        {
            case RoomType.Start:
                TutorialManager.Instance.ShowTip("move_attack",
                    "WASD 移动 · 鼠标左键射击 · Q/E/Z/X 释放技能");
                // 进入第一层（起始房）：默认呼出完整操作说明面板
                TutorialPanelController.Open();
                break;

            case RoomType.Normal:
                TutorialManager.Instance.ShowTip("combat_clear",
                    "击败房间内所有敌人，房门才会开启");
                break;

            case RoomType.Treasure:
                TutorialManager.Instance.ShowTip("treasure",
                    "宝箱房：清空敌人后拾取稀有道具");
                break;

            case RoomType.Boss:
                TutorialManager.Instance.ShowTip("boss",
                    "Boss 房：击败 Boss 即可开启通往下一层的出口");
                break;
        }
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

    /// <summary>解锁所有门 (启用触发器 + 禁用阻挡物)，跳过隐藏墙</summary>
    private void UnlockDoors()
    {
        foreach (var portal in _portals)
        {
            if (portal != null && portal.HiddenWallHP <= 0)
                portal.SetLocked(false);
        }
    }

    /// <summary>暂停/恢复所有刺陷阱（房间已清理时暂停，玩家进入时恢复）</summary>
    private void SetSpikesPaused(bool paused)
    {
        var spikes = GetComponentsInChildren<NerveSpike>(true);
        foreach (var s in spikes)
        {
            if (s == null) continue;
            if (paused) s.Pause();
            else s.Resume();
        }
    }

    /// <summary>生成怪物</summary>
    private void SpawnEnemies()
    {
        if (roomRoot == null || roomRoot.config == null) return;
        
        var cfg = roomRoot.config;

        // Boss 房在 hasBoss=false 时不生成敌人（空房间直接清场）
        if (cfg.roomType == RoomType.Boss)
        {
            var mapConfigAsset = MapManager.Instance?.MapConfigAsset;
            if (mapConfigAsset != null && !mapConfigAsset.hasBoss)
                return;
        }

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

            // 监听敌人死亡，使用 IEnemy 接口
            if (enemy.TryGetComponent<IEnemy>(out var ienemy))
            {
                ienemy.OnDied += () => OnEnemyDied(enemy);
                ienemy.OnAssimilated += (_) => OnEnemyAssimilated(enemy);
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

    /// <summary>清房判定是否已在排队（防多帧重复触发）</summary>
    private bool _clearCheckPending;

    /// <summary>
    /// 延迟一帧执行清房判定：等待同一死亡事件链中的亡语生成（如噬菌体/分裂怪）
    /// 完成 RegisterEnemy，避免"噬菌体还没注册、门已提前开启"。
    /// </summary>
    private void ScheduleClearCheck()
    {
        if (_clearCheckPending || _isCleared) return;
        if (_aliveEnemies.Count > 0) return;

        _clearCheckPending = true;
        StartCoroutine(DeferredClearCheck());
    }

    private System.Collections.IEnumerator DeferredClearCheck()
    {
        // 等一帧，让 OnAnyEnemyDied 订阅者（EnemyDeathSpawner 生成噬菌体等）执行完注册
        yield return null;

        _clearCheckPending = false;

        // 期间有新敌人注册 → 不清房（RegisterEnemy 不重置 pending，这里直接跳过即可）
        if (_aliveEnemies.Count > 0 || _isCleared) yield break;

        OnAllEnemiesDefeated();
    }

    /// <summary>敌人死亡回调</summary>
    private void OnEnemyDied(GameObject enemy)
    {
        RemoveAliveEnemy(enemy);

        // 生成货币掉落
        SpawnCurrency(enemy.transform.position, _currencyPerEnemy);

        ScheduleClearCheck();
    }

    /// <summary>敌人被同化为随从（不再计入房间清空判定）</summary>
    private void OnEnemyAssimilated(GameObject enemy)
    {
        RemoveAliveEnemy(enemy);

        ScheduleClearCheck();
    }

    /// <summary>
    /// 安全移除存活敌人 — List.Remove 依赖 EqualityComparer，Unity 对象销毁后
    /// ==重载返回 null 但比较器可能匹配失败，导致幽灵条目残留房间永远清不掉。
    /// 改用遍历 + Unity null 判断移除。
    /// </summary>
    private void RemoveAliveEnemy(GameObject enemy)
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            // 同时清理已销毁的幽灵条目
            if (_aliveEnemies[i] == null || _aliveEnemies[i] == enemy)
                _aliveEnemies.RemoveAt(i);
        }
    }

    /// <summary>
    /// 注册一个敌人到房间存活列表（亡语分裂/召唤生成的敌人也调用此方法），
    /// 否则亡语怪不计入 _aliveEnemies 会导致房间门提前开启。
    /// </summary>
    /// <summary>
    /// 注册一个敌人到房间存活列表（分裹/召唤生成的敌人也调用此方法）。
    /// 否则分裹不计入 _aliveEnemies 会导致房间门提前开启。
    /// </summary>
    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy == null || _aliveEnemies.Contains(enemy)) return;

        _aliveEnemies.Add(enemy);

        if (enemy.TryGetComponent<IEnemy>(out var ienemy))
        {
            ienemy.OnDied += () => OnEnemyDied(enemy);
            ienemy.OnAssimilated += (_) => OnEnemyAssimilated(enemy);
        }
    }

    /// <summary>所有敌人被击败</summary>
    private void OnAllEnemiesDefeated()
    {
        _isCleared = true;
        
        // 解锁门
        UnlockDoors();
        // 激活相邻隐藏房的门 (变为可破坏状态)
        ActivateHiddenWalls();
        // 暂停所有刺陷阱（固定状态0）
        SetSpikesPaused(true);

        // 共享生成点列表：道具先生成占点，血量回复物后生成用剩余点
        var availablePoints = new List<Transform>();
        if (roomRoot.itemSpawnPoints != null)
            availablePoints.AddRange(roomRoot.itemSpawnPoints);

        // 道具先生成
        SpawnItems(availablePoints);

        // 血量回复物后生成（用剩余点）
        SpawnHealthPickups(availablePoints);

        // Boss/Exit 房生成下一层出口
        TrySpawnNextLevelExit();

        Debug.Log($"Room {roomRoot.roomId}: 已清空!");

        // 统计：清空房间
        GameStatistics.Instance?.RecordRoomCleared(roomRoot.config.roomType);

        OnRoomCleared?.Invoke();
    }

    /// <summary>Boss/Exit 房清空后在房间中心生成下一层出口</summary>
    private void TrySpawnNextLevelExit()
    {
        if (roomRoot == null || roomRoot.config == null) return;
        if (roomRoot.config.roomType != RoomType.Boss) return;

        var mapConfigAsset = MapManager.Instance?.MapConfigAsset;
        if (mapConfigAsset == null) return;

        // 玩家站在出口区域 → 等玩家离开后再生成
        if (IsPlayerBlockingExit())
        {
            StartCoroutine(WaitForExitSpawn());
            return;
        }

        SpawnNextLevelExit();
    }

    /// <summary>真正实例化下一层出口</summary>
    private void SpawnNextLevelExit()
    {
        var mapConfigAsset = MapManager.Instance?.MapConfigAsset;
        if (mapConfigAsset == null) return;

        var exitGo = Instantiate(mapConfigAsset.nextLevelExitPrefab, roomRoot.Center, Quaternion.identity, transform);
        exitGo.name = "NextLevelExit";

        var exitComp = exitGo.GetComponent<NextLevelExit>();
        if (exitComp == null)
        {
            Debug.LogError("RoomManager: nextLevelExitPrefab 上没有挂载 NextLevelExit 组件，请在预制体上添加该脚本");
            return;
        }
        exitComp.nextSceneName = mapConfigAsset.nextSceneName;

        Debug.Log($"RoomManager: 房间 {roomRoot.roomId} 生成下一层出口 -> {mapConfigAsset.nextSceneName}");
    }

    /// <summary>玩家是否占用出口生成区域（房间中心）</summary>
    private bool IsPlayerBlockingExit()
    {
        var player = PlayerManager.Instance?.CurrentPlayer;
        if (player == null) return false;
        return Vector2.Distance(player.transform.position, roomRoot.Center) < _exitBlockRadius;
    }

    /// <summary>轮询等待玩家离开出口区域后生成出口（玩家消失则直接生成）</summary>
    private System.Collections.IEnumerator WaitForExitSpawn()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);

            var player = PlayerManager.Instance?.CurrentPlayer;
            if (player == null) break;   // 玩家不存在，直接生成
            if (Vector2.Distance(player.transform.position, roomRoot.Center) >= _exitBlockRadius)
                break;                    // 已离开区域
        }
        SpawnNextLevelExit();
    }

    /// <summary>激活通往相邻隐藏房的门</summary>
    private void ActivateHiddenWalls()
    {
        if (roomRoot == null) return;
        var graph = MapManager.Instance?.RoomGraph;
        if (graph == null) return;

        var node = graph.GetNode(roomRoot.roomId);
        if (node == null) return;

        foreach (var conn in node.connections)
        {
            var targetNode = graph.GetNode(conn.Key);
            if (targetNode == null || targetNode.roomType != RoomType.Hidden) continue;

            // 找到指向隐藏房的门 Portal
            var portals = GetComponentsInChildren<RoomPortal>(true);
            foreach (var portal in portals)
            {
                if (portal.targetRoomId == conn.Key && portal.HiddenWallHP > 0)
                {
                    portal.SetBreakable();
                    break;
                }
            }
        }
    }

    /// <summary>生成道具（概率+权重，占点后从 availablePoints 移除）</summary>
    private void SpawnItems(List<Transform> availablePoints)
    {
        if (roomRoot == null || roomRoot.config == null) return;
        var cfg = roomRoot.config;

        if (availablePoints == null || availablePoints.Count == 0) return;

        // 概率判定
        if (Random.value > cfg.itemSpawnChance) return;

        // 权重抽取生成数量
        int count = GetWeightedRandomCount(cfg.itemCountWeights);
        count = Mathf.Min(count, availablePoints.Count);
        if (count <= 0) return;

        var library = ItemsLibrary.Instance;
        if (library == null)
        {
            Debug.LogWarning("[RoomManager] ItemsLibrary 未找到，跳过道具生成");
            return;
        }

        var pool = (cfg.itemPool != null && cfg.itemPool.Count > 0) ? cfg.itemPool : null;
        var weights = (cfg.qualityWeights != null && cfg.qualityWeights.Length > 0) ? cfg.qualityWeights : null;
        var filter = ItemPoolFilter.GetPlayerItemManager();

        var picked = library.GetRandomItemPrefabs(count, pool, weights, filter);
        for (int i = 0; i < picked.Count && availablePoints.Count > 0; i++)
        {
            if (picked[i] == null) continue;
            int idx = Random.Range(0, availablePoints.Count);
            Instantiate(picked[i], availablePoints[idx].position, Quaternion.identity, transform);
            availablePoints.RemoveAt(idx);
        }

        // Boss 房必定生成一个溶酶体（使用无副作用方法，不影响随机池）
        if (roomRoot.config.roomType == RoomType.Boss)
        {
            var lysosomePrefab = library.GetItemPrefabDirectly("lysosome");
            if (lysosomePrefab != null && availablePoints.Count > 0)
            {
                int idx = Random.Range(0, availablePoints.Count);
                Instantiate(lysosomePrefab, availablePoints[idx].position, Quaternion.identity, transform);
                availablePoints.RemoveAt(idx);
            }
        }
    }

    /// <summary>生成血量回复物（清房后概率+权重，用道具剩余的生成点）</summary>
    private void SpawnHealthPickups(List<Transform> availablePoints)
    {
        if (roomRoot == null || roomRoot.config == null) return;
        var cfg = roomRoot.config;

        if (availablePoints == null || availablePoints.Count == 0) return;
        if (cfg.healthPickupPrefab == null) return;

        // 概率判定
        if (Random.value > cfg.healthPickupChance) return;

        // 权重抽取生成数量
        int count = GetWeightedRandomCount(cfg.healthPickupCountWeights);
        count = Mathf.Min(count, availablePoints.Count);
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            if (availablePoints.Count == 0) break;
            int idx = Random.Range(0, availablePoints.Count);
            Instantiate(cfg.healthPickupPrefab, availablePoints[idx].position, Quaternion.identity, transform);
            availablePoints.RemoveAt(idx);
        }
    }

    /// <summary>加权随机抽取生成数量（索引i → 生成i+1个）</summary>
    private int GetWeightedRandomCount(int[] weights)
    {
        if (weights == null || weights.Length == 0) return 0;
        int totalWeight = 0;
        foreach (int w in weights) totalWeight += w;
        if (totalWeight <= 0) return 0;
        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return i + 1;
        }
        return weights.Length;
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

    // ==================== Shop 房间 ====================

    /// <summary>开张商店</summary>
    private void OpenShopRoom()
    {
        var shopManager = GetComponent<ShopManager>();
        if (shopManager == null)
        {
            Debug.LogWarning($"[RoomManager] Room{roomRoot.roomId}: Shop 房间但未挂载 ShopManager 组件");
            return;
        }

        shopManager.OpenShop();
    }

    /// <summary>打烊商店</summary>
    private void CloseShopRoom()
    {
        var shopManager = GetComponent<ShopManager>();
        if (shopManager != null)
            shopManager.CloseShop();
    }

    /// <summary>在指定位置生成货币掉落物</summary>
    private void SpawnCurrency(Vector3 position, int amount)
    {
        var go = new GameObject("CurrencyPickup", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(CurrencyPickup));

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = _currencyIcon;
        sr.sortingOrder = 5;

        var col = go.GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.4f;

        var pickup = go.GetComponent<CurrencyPickup>();
        pickup.amount = amount;

        // 随机散布
        position += (Vector3)(Random.insideUnitCircle * 0.5f);
        go.transform.position = position;
        go.transform.SetParent(transform);
    }
}
