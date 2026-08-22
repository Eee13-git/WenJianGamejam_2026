using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具池过滤工具 — 道具刷新时排除玩家已达拾取上限的道具。
/// 玩家持有某道具数量 >= ItemData.maxCount（且 maxCount>0）时，该道具不再从池中刷新。
/// 供 RoomManager（房间刷新）与 ShopManager（商店开张）共用。
/// </summary>
public static class ItemPoolFilter
{
    /// <summary>
    /// 过滤道具池：移除玩家已达上限的道具预制体。返回过滤后的新列表。
    /// itemManager 为空或池为空时原样返回。
    /// </summary>
    public static List<GameObject> GetAvailablePool(IList<GameObject> pool, ItemManager itemManager)
    {
        if (pool == null || pool.Count == 0) return new List<GameObject>(pool ?? System.Array.Empty<GameObject>());
        if (itemManager == null) return new List<GameObject>(pool);

        var result = new List<GameObject>(pool.Count);
        foreach (var prefab in pool)
        {
            if (prefab == null) continue;

            // 从预制体上取 ItemData
            var pickup = prefab.GetComponent<ItemPickup>();
            if (pickup == null || pickup.itemData == null)
            {
                // 无法判断的道具保留（不误删）
                result.Add(prefab);
                continue;
            }

            var data = pickup.itemData;
            // 无上限（maxCount=0）或未达上限 → 保留
            if (data.maxCount <= 0 || itemManager.GetItemCount(data.itemId) < data.maxCount)
                result.Add(prefab);
        }
        return result;
    }

    /// <summary>统一获取玩家的 ItemManager（PlayerManager 持久化单例优先）</summary>
    public static ItemManager GetPlayerItemManager()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
            return PlayerManager.Instance.CurrentPlayer.GetComponent<ItemManager>();

        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<ItemManager>() : null;
    }
}
