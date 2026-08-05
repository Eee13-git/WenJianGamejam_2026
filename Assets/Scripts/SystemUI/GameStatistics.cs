using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏统计单例 (DontDestroyOnLoad) — 追踪一局游戏的所有累计数据。
/// 
/// 生命周期:
///   Start 场景 → 销毁
///   游戏场景   → 创建/重置
///   Result 场景 → 保留 (供结算UI读取)
/// 
/// 订阅各系统事件，累计:
///   - 获得 / 消费的货币
///   - 获得的道具列表（含重复获取次数）
///   - 各类型房间的访问/清空数
///   - 各类型敌人的击杀数
/// </summary>
public class GameStatistics : MonoBehaviour
{
    public static GameStatistics Instance { get; private set; }

    // ========== 货币 ==========
    public int TotalCurrencyEarned { get; private set; }
    public int TotalCurrencySpent { get; private set; }

    // ========== 道具 ==========
    private readonly Dictionary<string, (ItemData data, int count)> _acquiredItems = new();
    /// <summary>累计获得的道具 (itemId → (ItemData, 获取次数))</summary>
    public IReadOnlyDictionary<string, (ItemData data, int count)> AcquiredItems => _acquiredItems;

    // ========== 房间 ==========
    private readonly Dictionary<RoomType, int> _roomsVisitedByType = new();
    private readonly Dictionary<RoomType, int> _roomsClearedByType = new();

    /// <summary>各房间类型的进入次数</summary>
    public IReadOnlyDictionary<RoomType, int> RoomsVisitedByType => _roomsVisitedByType;
    /// <summary>各房间类型的清空次数</summary>
    public IReadOnlyDictionary<RoomType, int> RoomsClearedByType => _roomsClearedByType;

    /// <summary>总进入房间数</summary>
    public int RoomsVisited { get; private set; }
    /// <summary>总清空房间数</summary>
    public int RoomsCleared { get; private set; }

    // ========== 击杀 ==========
    private readonly Dictionary<string, int> _enemiesKilled = new();
    /// <summary>敌人显示名 → 击杀数</summary>
    public IReadOnlyDictionary<string, int> EnemiesKilled => _enemiesKilled;

    // ========== 内部 ==========
    private CurrencyManager _boundCurrency;

    // ==================== 生命周期 ====================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EnemyCore.OnAnyEnemyDied += OnEnemyDied;
    }

    private void OnDisable()
    {
        UnbindAll();
        EnemyCore.OnAnyEnemyDied -= OnEnemyDied;
    }

    private void OnDestroy()
    {
        UnbindAll();
        if (Instance == this)
            Instance = null;
    }

    // ==================== 绑定 / 解绑 ====================

    /// <summary>绑定 Player 身上的组件事件（货币/道具）</summary>
    public void BindPlayer(GameObject player)
    {
        if (player == null) return;

        var cm = player.GetComponent<CurrencyManager>();
        if (cm != null)
        {
            cm.OnCurrencyEarned += OnCurrencyEarned;
            cm.OnCurrencySpent += OnCurrencySpent;
            _boundCurrency = cm;
        }

        var im = player.GetComponent<ItemManager>();
        if (im != null)
        {
            im.OnItemAcquired += OnItemAcquired;
        }
    }

    /// <summary>解绑所有 Player 事件</summary>
    private void UnbindAll()
    {
        if (_boundCurrency != null)
        {
            _boundCurrency.OnCurrencyEarned -= OnCurrencyEarned;
            _boundCurrency.OnCurrencySpent -= OnCurrencySpent;
            _boundCurrency = null;
        }
    }

    // ==================== 事件回调 ====================

    private void OnCurrencyEarned(int amount)
    {
        TotalCurrencyEarned += amount;
    }

    private void OnCurrencySpent(int amount)
    {
        TotalCurrencySpent += amount;
    }

    private void OnItemAcquired(ItemData item)
    {
        if (item == null) return;

        string id = string.IsNullOrEmpty(item.itemId) ? item.name : item.itemId;
        if (_acquiredItems.TryGetValue(id, out var entry))
            _acquiredItems[id] = (entry.data, entry.count + 1);
        else
            _acquiredItems[id] = (item, 1);
    }

    private void OnEnemyDied(EnemyCore enemy)
    {
        if (enemy == null) return;

        string enemyName;
        if (enemy.config != null)
        {
            enemyName = !string.IsNullOrEmpty(enemy.config.displayName)
                ? enemy.config.displayName
                : enemy.config.name;
        }
        else
        {
            enemyName = enemy.name;
        }

        _enemiesKilled.TryGetValue(enemyName, out int count);
        _enemiesKilled[enemyName] = count + 1;
    }

    // ==================== 房间 ====================

    public void RecordRoomVisited(RoomType roomType)
    {
        RoomsVisited++;
        _roomsVisitedByType.TryGetValue(roomType, out int v);
        _roomsVisitedByType[roomType] = v + 1;
    }

    public void RecordRoomCleared(RoomType roomType)
    {
        RoomsCleared++;
        _roomsClearedByType.TryGetValue(roomType, out int v);
        _roomsClearedByType[roomType] = v + 1;
    }

    // ==================== 重置 ====================

    /// <summary>清空所有统计数据（新一局开始时调用）</summary>
    public void ResetAll()
    {
        TotalCurrencyEarned = 0;
        TotalCurrencySpent = 0;
        _acquiredItems.Clear();
        RoomsVisited = 0;
        RoomsCleared = 0;
        _roomsVisitedByType.Clear();
        _roomsClearedByType.Clear();
        _enemiesKilled.Clear();
    }
}
