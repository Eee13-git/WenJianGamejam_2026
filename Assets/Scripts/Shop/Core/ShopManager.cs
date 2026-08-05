using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 商店管理器 — 挂载在 Shop 房间预制体的根节点上。
///
/// 与其他房间一样，道具从 RoomConfig.itemPool 生成。
/// 区别在于：生成的 ItemPickup 打上 IsShopItem=true 标记，
/// ItemInteractionHandler 靠近时会显示购买 UI 而非拾取 UI。
///
/// 流程:
///   OpenShop()  ─→  从 RoomConfig.itemPool 随机抽 slotCount 个，在 spawn point 生成并标记 IsShopItem
///   CloseShop() ─→  清理所有未购买的拾取物
/// </summary>
public class ShopManager : MonoBehaviour
{
    [Header("商店参数")]
    [Tooltip("每次开张生成的商品数量")]
    [Range(1, 12)] public int slotCount = 4;

    [Header("打折（可选）")]
    [Range(0f, 1f)] public float discount = 0f;

    /// <summary>商店是否已开张</summary>
    public bool IsOpen { get; private set; }

    /// <summary>已生成的拾取物数量</summary>
    public int SpawnedCount => _spawnedPickups.Count;

    // ========== 事件 ==========

    /// <summary>任意 ShopManager 实例化就绪时触发</summary>
    public static event Action<ShopManager> OnAnyShopManagerReady;

    /// <summary>所有商品被买空</summary>
    public event Action OnAllItemsSold;

    // ========== 内部状态 ==========

    private readonly List<ItemPickup> _spawnedPickups = new();

    private void Awake()
    {
        OnAnyShopManagerReady?.Invoke(this);
    }

    private void OnDestroy()
    {
        CloseShop();
    }

    /// <summary>开张 — 在物品生成点生成带商店标记的拾取物</summary>
    public void OpenShop()
    {
        var roomRoot = GetComponent<RoomRoot>();
        if (roomRoot == null || roomRoot.config == null)
        {
            Debug.LogError("[ShopManager] RoomRoot 或 RoomConfig 不存在！", this);
            return;
        }

        var cfg = roomRoot.config;
        if (cfg.itemPool == null || cfg.itemPool.Count == 0)
        {
            Debug.LogWarning("[ShopManager] RoomConfig 道具池为空，无法开张！", this);
            return;
        }

        CloseShop(); // 清除残留

        // 过滤：玩家已达拾取上限的道具不再上架
        var available = ItemPoolFilter.GetAvailablePool(cfg.itemPool, ItemPoolFilter.GetPlayerItemManager());
        if (available.Count == 0)
        {
            Debug.Log("[ShopManager] 道具池中所有道具均已达上限，商店无法开张");
            IsOpen = false;
            return;
        }
        Shuffle(available);

        int count = Mathf.Min(slotCount, available.Count);

        var spawnPoints = roomRoot.itemSpawnPoints;
        int spawnPointCount = spawnPoints != null ? spawnPoints.Length : 0;

        if (spawnPointCount == 0)
            Debug.LogWarning("[ShopManager] 房间没有物品生成点，将在房间中心散开生成", this);

        for (int i = 0; i < count; i++)
        {
            var prefab = available[i];
            if (prefab == null) continue;

            // 确定生成位置（和 RoomManager.SpawnItems 一样用 itemSpawnPoints）
            Vector3 spawnPos;
            if (i < spawnPointCount && spawnPoints[i] != null)
                spawnPos = spawnPoints[i].position;
            else
                spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * 2f);

            var go = Instantiate(prefab, spawnPos, Quaternion.identity, transform);
            go.name = $"ShopItem_{prefab.name}";

            // 打上商店标记
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = go.AddComponent<ItemPickup>();

            pickup.IsShopItem = true;

            _spawnedPickups.Add(pickup);
        }

        IsOpen = true;
        Debug.Log($"[ShopManager] 商店开张，生成了 {_spawnedPickups.Count} 件商品 (从 RoomConfig.itemPool)");
    }

    /// <summary>打烊 — 清理所有未购买的拾取物</summary>
    public void CloseShop()
    {
        IsOpen = false;

        foreach (var pickup in _spawnedPickups)
        {
            if (pickup != null && pickup.gameObject != null)
                Destroy(pickup.gameObject);
        }
        _spawnedPickups.Clear();
    }

    /// <summary>拾取物被购买后回调（由 ItemInteractionHandler 调用）</summary>
    public void NotifyItemPurchased(ItemPickup pickup)
    {
        if (pickup == null) return;
        _spawnedPickups.Remove(pickup);

        Debug.Log($"[ShopManager] 售出: {pickup.itemData?.itemName}");

        _spawnedPickups.RemoveAll(p => p == null);
        if (_spawnedPickups.Count == 0 && IsOpen)
        {
            IsOpen = false;
            OnAllItemsSold?.Invoke();
        }
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
