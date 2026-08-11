using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具管理器 — 管理玩家当前持有的所有道具。
/// 挂载在 Player GameObject 上，通过 PlayerManager 的持久化机制自动跟随跨场景。
///
/// 核心职责:
///   1. 管理道具集合（添加/移除/查询/丢弃/转移）
///   2. 执行道具效果（OnAcquire / OnRemove）
///   3. 通知事件（供 UI Controller 监听）
/// </summary>
public class ItemManager : MonoBehaviour
{
    [Header("丢弃配置")]
    [SerializeField] private GameObject _itemDropPrefab; // 丢弃道具时生成的拾取物预制体

    [Header("调试")]
    [SerializeField] private List<ItemData> _initialItems; // Inspector 调试用

    private readonly List<ItemData> _items = new List<ItemData>();
    private readonly Dictionary<string, ItemData> _itemDict = new Dictionary<string, ItemData>();
    private readonly Dictionary<string, int> _itemCounts = new Dictionary<string, int>();

    /// <summary>当前道具列表（只读），每种道具只出现一次</summary>
    public IReadOnlyList<ItemData> Items => _items;

    /// <summary>道具种类数量</summary>
    public int Count => _items.Count;

    // ========== 事件 ==========

    /// <summary>道具获得: (itemData)</summary>
    public event Action<ItemData> OnItemAcquired;

    /// <summary>道具移除: (itemData)</summary>
    public event Action<ItemData> OnItemRemoved;

    /// <summary>道具丢弃: (itemData, worldPosition)</summary>
    public event Action<ItemData, Vector3> OnItemDropped;

    /// <summary>道具转移: 从 (fromOwner, itemData) 转移到 (toOwner, itemData)</summary>
    public static event Action<ItemManager, ItemManager, ItemData> OnItemTransferred;

    /// <summary>任意道具被获取后触发（供 Buff 检查三件套等）</summary>
    public static event Action<GameObject> OnAnyItemAcquired;

    // ========== Unity 生命周期 ==========

    private void Start()
    {
        // Inspector 调试：自动获得初始道具
        foreach (var item in _initialItems)
        {
            if (item != null)
                AcquireItem(item);
        }
    }

    // ========== 核心方法 ==========

    /// <summary>
    /// 获得道具 — 每次获取都执行效果（支持叠加）。
    /// maxCount=0 表示无上限。
    /// </summary>
    public bool AcquireItem(ItemData item)
    {
        if (item == null) return false;

        int max = item.maxCount;
        bool hasItem = _itemDict.ContainsKey(item.itemId);
        int currentCount = hasItem ? _itemCounts[item.itemId] : 0;

        // 达到上限
        if (max > 0 && currentCount >= max)
            return false;

        // 每次获取都执行效果
        item.effect?.OnAcquire(gameObject);

        if (!hasItem)
        {
            _items.Add(item);
            _itemDict[item.itemId] = item;
            _itemCounts[item.itemId] = 1;
        }
        else
        {
            _itemCounts[item.itemId] = currentCount + 1;
        }

        OnItemAcquired?.Invoke(item);
        OnAnyItemAcquired?.Invoke(gameObject);
        return true;
    }

    /// <summary>
    /// 移除道具 — 每次移除都执行清理（支持叠加效果逐层移除）。
    /// 数量归零时从集合移除。
    /// </summary>
    public bool RemoveItem(string itemId)
    {
        if (!_itemDict.TryGetValue(itemId, out var item))
            return false;

        // 每次移除都执行清理
        item.effect?.OnRemove(gameObject);

        int count = _itemCounts[itemId];
        if (count <= 1)
        {
            _items.Remove(item);
            _itemDict.Remove(itemId);
            _itemCounts.Remove(itemId);
        }
        else
        {
            _itemCounts[itemId] = count - 1;
        }

        OnItemRemoved?.Invoke(item);
        return true;
    }

    /// <summary>
    /// 丢弃道具 — 在世界坐标生成可拾取的掉落物。
    /// 与 RemoveItem 的区别：丢弃会在场景中生成 ItemPickup GameObject。
    /// </summary>
    public bool DropItem(string itemId, Vector3 worldPosition)
    {
        if (!_itemDict.TryGetValue(itemId, out var item))
            return false;

        // 每次丢弃都执行清理
        item.effect?.OnRemove(gameObject);

        int count = _itemCounts[itemId];
        if (count <= 1)
        {
            _items.Remove(item);
            _itemDict.Remove(itemId);
            _itemCounts.Remove(itemId);
        }
        else
        {
            _itemCounts[itemId] = count - 1;
        }

        // 在世界坐标生成掉落物
        SpawnItemPickup(item, worldPosition);

        OnItemDropped?.Invoke(item, worldPosition);
        OnItemRemoved?.Invoke(item);
        return true;
    }

    /// <summary>
    /// 丢弃道具在玩家脚下
    /// </summary>
    public bool DropItemAtPlayer(string itemId)
    {
        return DropItem(itemId, transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.5f));
    }

    /// <summary>
    /// 将道具转移给另一个拥有者。
    /// 从当前所有者的集合移除，添加到目标所有者的集合。
    /// 用于交易 / 队友共享道具等场景。
    /// </summary>
    public bool TransferItem(string itemId, ItemManager targetOwner)
    {
        if (targetOwner == null || !_itemDict.TryGetValue(itemId, out var item))
            return false;

        // 目标不能已有同 ID 道具且已达上限
        if (targetOwner.HasItem(itemId))
        {
            int targetMax = item.maxCount;
            int targetCount = targetOwner.GetItemCount(itemId);
            if (targetMax > 0 && targetCount >= targetMax)
                return false;
        }

        // 从当前所有者移除一份
        item.effect?.OnRemove(gameObject);

        int count = _itemCounts[itemId];
        if (count <= 1)
        {
            item.effect?.OnRemove(gameObject);
            _items.Remove(item);
            _itemDict.Remove(itemId);
            _itemCounts.Remove(itemId);
        }
        else
        {
            _itemCounts[itemId] = count - 1;
        }

        // 添加到目标
        targetOwner.AcquireItemInternal(item);

        OnItemTransferred?.Invoke(this, targetOwner, item);
        OnItemRemoved?.Invoke(item);
        return true;
    }

    /// <summary>内部获得道具（供 TransferItem 使用）</summary>
    private void AcquireItemInternal(ItemData item)
    {
        bool hasItem = _itemDict.ContainsKey(item.itemId);

        item.effect?.OnAcquire(gameObject);

        if (!hasItem)
        {
            _items.Add(item);
            _itemDict[item.itemId] = item;
            _itemCounts[item.itemId] = 1;
        }
        else
        {
            _itemCounts[item.itemId] = _itemCounts[item.itemId] + 1;
        }

        OnItemAcquired?.Invoke(item);
    }

    /// <summary>是否有指定道具</summary>
    public bool HasItem(string itemId) => _itemDict.ContainsKey(itemId);

    /// <summary>获取道具</summary>
    public ItemData GetItem(string itemId)
        => _itemDict.TryGetValue(itemId, out var item) ? item : null;

    /// <summary>获取道具持有数量（不存在返回0）</summary>
    public int GetItemCount(string itemId)
        => _itemCounts.TryGetValue(itemId, out var count) ? count : 0;

    // ========== 私有方法 ==========

    /// <summary>在指定世界位置生成可拾取的道具掉落物</summary>
    private void SpawnItemPickup(ItemData item, Vector3 position)
    {
        GameObject prefab = _itemDropPrefab;
        if (prefab == null)
        {
            // 如果没有配置掉落预制体，创建一个默认的
            prefab = CreateDefaultPickup();
        }

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        go.name = $"ItemPickup_{item.itemId}";

        var pickup = go.GetComponent<ItemPickup>();
        if (pickup == null)
            pickup = go.AddComponent<ItemPickup>();
        pickup.itemData = item;
    }

    /// <summary>创建默认拾取物预制体（运行时兜底）</summary>
    private GameObject CreateDefaultPickup()
    {
        var go = new GameObject("DefaultItemPickup", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(ItemPickup));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = null; // 使用默认图标
        sr.drawMode = SpriteDrawMode.Simple;
        var col = go.GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        return go;
    }
}
