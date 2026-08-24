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

    /// <summary>全局额外折扣（科技树等），0=无额外折扣，0.05=额外95折</summary>
    public static float GlobalDiscount = 0f;

    /// <summary>实际折扣 = 实例折扣 + 全局折扣</summary>
    public float EffectiveDiscount => Mathf.Clamp01(discount + GlobalDiscount);

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

        // 新手引导：首次进入商店提示
        TutorialManager.Instance?.ShowTip("shop_first",
            "商店：靠近商品查看价格，花费 ATP 购买道具");

        var cfg = roomRoot.config;

        var library = ItemsLibrary.Instance;
        if (library == null)
        {
            Debug.LogWarning("[ShopManager] ItemsLibrary 未找到，无法开张");
            return;
        }

        CloseShop();

        // 确定池和权重
        var pool = (cfg.itemPool != null && cfg.itemPool.Count > 0) ? cfg.itemPool : null;
        var weights = (cfg.qualityWeights != null && cfg.qualityWeights.Length > 0) ? cfg.qualityWeights : null;
        var filter = ItemPoolFilter.GetPlayerItemManager();

        var picked = library.GetRandomItemPrefabs(slotCount, pool, weights, filter);
        if (picked.Count == 0)
        {
            Debug.Log("[ShopManager] 道具池为空或全部已达上限，商店无法开张");
            IsOpen = false;
            return;
        }

        var spawnPoints = roomRoot.itemSpawnPoints;
        int spawnPointCount = spawnPoints != null ? spawnPoints.Length : 0;

        for (int i = 0; i < picked.Count; i++)
        {
            var prefab = picked[i];
            if (prefab == null) continue;

            Vector3 spawnPos;
            if (i < spawnPointCount && spawnPoints[i] != null)
                spawnPos = spawnPoints[i].position;
            else
                spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * 2f);

            var go = Instantiate(prefab, spawnPos, Quaternion.identity, transform);
            go.name = $"ShopItem_{prefab.name}";

            var pickup = go.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = go.AddComponent<ItemPickup>();
            pickup.IsShopItem = true;

            _spawnedPickups.Add(pickup);
        }

        IsOpen = true;
        Debug.Log($"[ShopManager] 商店开张，生成了 {_spawnedPickups.Count} 件商品");
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
}
