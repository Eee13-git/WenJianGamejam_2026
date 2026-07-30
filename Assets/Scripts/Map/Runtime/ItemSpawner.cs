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

        var pool = roomConfig != null ? roomConfig.itemPool : 
            (manualItemPool != null ? new System.Collections.Generic.List<GameObject>(manualItemPool) : null);

        if (pool == null || pool.Count == 0) return;

        int min = roomConfig != null ? roomConfig.minItems : manualMinItems;
        int max = roomConfig != null ? roomConfig.maxItems : manualMaxItems;
        int total = Random.Range(min, max + 1);
        int available = spawnPoints != null ? spawnPoints.Length : 0;

        if (available == 0) return;

        int count = Mathf.Min(total, available);

        // 随机洗牌但不重复点
        var indices = new int[available];
        for (int i = 0; i < available; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, pool.Count);
            var itemPrefab = pool[idx];
            if (itemPrefab == null) continue;

            Instantiate(itemPrefab, spawnPoints[indices[i]].position, Quaternion.identity, transform);
        }
    }
}
