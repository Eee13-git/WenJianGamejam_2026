using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具生成器 — 从道具池中随机生成道具
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Header("道具池")]
    [Tooltip("引用房间配置")]
    public RoomConfig roomConfig;

    [Tooltip("手动配置 — 仅在没有 roomConfig 时使用")]
    public GameObject[] manualItemPool;
    public int manualMinItems = 1;
    public int manualMaxItems = 3;

    [Header("生成点")]
    public Transform[] spawnPoints;

    private bool _hasSpawned;

    /// <summary>生成道具</summary>
    public void Spawn()
    {
        if (_hasSpawned) return;
        _hasSpawned = true;

        var library = ItemsLibrary.Instance;
        if (library == null)
        {
            Debug.LogWarning("[ItemSpawner] ItemsLibrary 未找到，跳过生成");
            return;
        }

        // 确定池和权重
        var pool = (roomConfig != null && roomConfig.itemPool != null && roomConfig.itemPool.Count > 0)
            ? roomConfig.itemPool
            : (manualItemPool != null && manualItemPool.Length > 0 ? new List<GameObject>(manualItemPool) : null);
        var weights = (roomConfig != null && roomConfig.qualityWeights != null && roomConfig.qualityWeights.Length > 0)
            ? roomConfig.qualityWeights
            : null;
        var filter = ItemPoolFilter.GetPlayerItemManager();

        int min = roomConfig != null ? roomConfig.minItems : manualMinItems;
        int max = roomConfig != null ? roomConfig.maxItems : manualMaxItems;
        int total = Random.Range(min, max + 1);
        int available = spawnPoints != null ? spawnPoints.Length : 0;

        if (available == 0) return;

        int count = Mathf.Min(total, available);

        // 随机洗牌生成点
        var indices = new int[available];
        for (int i = 0; i < available; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var picked = library.GetRandomItemPrefabs(count, pool, weights, filter);
        for (int i = 0; i < picked.Count; i++)
        {
            if (picked[i] == null) continue;
            Instantiate(picked[i], spawnPoints[indices[i]].position, Quaternion.identity, transform);
        }
    }
}
