using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具库（ScriptableObject）— 所有道具生成的中央管理入口。
/// 右键 -> Create -> Game -> Items Library 创建。
///
/// 核心机制:
///   1. 不放回抽取：道具每一局只能生成一次，已生成的从池中移除
///   2. 池空自动重置：所有道具都被抽取后，重新从母本拷贝
///   3. 品质加权随机：先按权重随机出品质 → 再从该品质中等概率选一个
///   4. 优先池机制：RoomConfig.itemPool 作为优先池，从中均匀随机选道具，
///      若该道具在 Library 池中可用则直接取出，否则回退到品质加权随机
///   5. 集成 ItemPoolFilter（过滤玩家已达上限的道具）
/// </summary>
[CreateAssetMenu(fileName = "ItemsLibrary", menuName = "Game/Items Library")]
public class ItemsLibrary : ScriptableObject
{
    [Header("道具预制体")]
    [Tooltip("全量道具预制体列表（每个须挂载 ItemPickup 组件）")]
    [SerializeField] private List<GameObject> _itemPrefabs;

    [Header("默认品质权重")]
    [Tooltip("全局默认品质生成权重。RoomConfig 可覆盖。")]
    [SerializeField] private QualityWeight[] _defaultQualityWeights = new[]
    {
        new QualityWeight { quality = ItemQuality.Common,    weight = 50f },
        new QualityWeight { quality = ItemQuality.Uncommon,  weight = 30f },
        new QualityWeight { quality = ItemQuality.Rare,      weight = 15f },
        new QualityWeight { quality = ItemQuality.Legendary, weight = 5f  },
    };

    /// <summary>全量道具预制体（只读）</summary>
    public IReadOnlyList<GameObject> AllItemPrefabs => _itemPrefabs;

    /// <summary>默认品质权重（只读）</summary>
    public IReadOnlyList<QualityWeight> DefaultQualityWeights => _defaultQualityWeights;

    /// <summary>当前可用水池数量</summary>
    public int AvailableCount => _availablePool != null ? _availablePool.Count : 0;

    // ========== 运行时状态 ==========

    /// <summary>运行时可用水池（不放回抽取，池空自动重置）</summary>
    private List<GameObject> _availablePool;

    /// <summary>标记运行时是否已初始化</summary>
    private bool _initialized;

    /// <summary>局内品质权重偏移（道具效果可动态修改，ResetPool 时清除）</summary>
    private Dictionary<ItemQuality, float> _qualityWeightOffsets;

    /// <summary>局内被禁品质（道具效果可永久移除某品质，池空重置也不添加，新一局清除）</summary>
    private HashSet<ItemQuality> _bannedQualities;

    // ========== 静态访问 ==========

    private static ItemsLibrary _instance;

    /// <summary>
    /// 从 Resources 加载的全局实例（缓存）。
    /// 资产路径：Resources/TestAssets/ItemsLibrary.asset
    /// </summary>
    public static ItemsLibrary Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ItemsLibrary>("TestAssets/ItemsLibrary");
            return _instance;
        }
    }

    // ========== 生命周期 ==========

    /// <summary>
    /// 进入 Play Mode 时重置道具池。
    /// 确保 Domain Reloading 禁用时上一局的状态不会残留。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnPlayModeStart()
    {
        // 强制重新加载实例并重置（Domain Reloading 禁用时 _instance 可能残留旧引用）
        _instance = Resources.Load<ItemsLibrary>("TestAssets/ItemsLibrary");
        if (_instance != null)
            _instance.ResetPool();
    }

    /// <summary>确保运行时池已初始化</summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        ResetPool();
    }

    /// <summary>
    /// 重置道具池 — 从母本拷贝全量道具。
    /// 供游戏流程在新一局开始时调用。
    /// </summary>
    public void ResetPool()
    {
        _bannedQualities = new HashSet<ItemQuality>();
        _qualityWeightOffsets = new Dictionary<ItemQuality, float>();
        RebuildPool();
        _initialized = true;
    }

    /// <summary>
    /// 从母本重建可用水池，排除被禁品质。不清除禁集和偏移。
    /// 供池空自动重置和 ResetPool 共用。
    /// </summary>
    private void RebuildPool()
    {
        _availablePool = new List<GameObject>();
        if (_itemPrefabs == null) return;

        foreach (var prefab in _itemPrefabs)
        {
            if (prefab == null) continue;
            if (_bannedQualities != null && _bannedQualities.Count > 0)
            {
                var pickup = prefab.GetComponent<ItemPickup>();
                if (pickup != null && pickup.itemData != null && _bannedQualities.Contains(pickup.itemData.quality))
                    continue;
            }
            _availablePool.Add(prefab);
        }
    }

    // ========== 核心 API ==========

    /// <summary>
    /// 抽取一个道具预制体（不放回）。
    ///
    /// 优先池逻辑：
    ///   1. 若 preferredPool 非空，从中均匀随机选一个，并弹出
    ///   2. 尝试从 Library 池中取出该道具（TryGetSpecific）
    ///   3. 若 Library 池中没有（已被生成过），回退到品质加权随机
    ///
    /// 若 preferredPool 为空，直接品质加权随机。
    /// 池空自动重置。
    /// </summary>
    /// <param name="preferredPool">优先池（RoomConfig.itemPool），null 则用全池加权随机</param>
    /// <param name="weights">品质权重覆盖（null 则用默认权重）</param>
    /// <param name="filter">ItemManager 用于过滤已达上限的道具（null 则不过滤）</param>
    public GameObject GetRandomItemPrefab(
        IList<GameObject> preferredPool = null,
        QualityWeight[] weights = null,
        ItemManager filter = null)
    {
        var list = GetRandomItemPrefabs(1, preferredPool, weights, filter);
        return list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// 抽取 N 个不重复的道具预制体（不放回）。
    ///
    /// 优先池逻辑（每次独立）：
    ///   1. 若 preferredPool 副本非空，从中均匀随机选一个并弹出
    ///   2. 尝试从 Library 池中取出该道具
    ///   3. 若不可用，回退到品质加权随机
    ///
    /// 池空自动重置继续抽取。
    /// </summary>
    public List<GameObject> GetRandomItemPrefabs(
        int count,
        IList<GameObject> preferredPool = null,
        QualityWeight[] weights = null,
        ItemManager filter = null)
    {
        EnsureInitialized();

        var result = new List<GameObject>();

        // 拷贝优先池（运行时可修改，不影响 RoomConfig 原始数据）
        var remaining = (preferredPool != null && preferredPool.Count > 0)
            ? new List<GameObject>(preferredPool)
            : null;

        for (int i = 0; i < count; i++)
        {
            GameObject picked = null;

            // 1. 尝试从优先池中选
            if (remaining != null && remaining.Count > 0)
            {
                int idx = Random.Range(0, remaining.Count);
                var preferred = remaining[idx];
                remaining.RemoveAt(idx); // 弹出（无论是否成功取出）

                // 尝试从 Library 池中取出该指定道具
                picked = TryGetSpecific(preferred, filter);
            }

            // 2. 优先池不可用或无优先池 → 品质加权随机
            if (picked == null)
                picked = WeightedRandomDraw(weights, filter);

            if (picked == null) break;
            result.Add(picked);
        }

        return result;
    }

    /// <summary>
    /// 按 itemId 直接获取道具预制体（无副作用）。
    /// 不检查运行时池、不从池中移除、不影响不放回抽取逻辑。
    /// 用于 Boss 房等需要必定生成指定道具的场景。
    /// </summary>
    public GameObject GetItemPrefabDirectly(string itemId)
    {
        if (_itemPrefabs == null) return null;
        foreach (var prefab in _itemPrefabs)
        {
            if (prefab == null) continue;
            var pickup = prefab.GetComponent<ItemPickup>();
            if (pickup != null && pickup.itemData != null
                && pickup.itemData.itemId == itemId)
                return prefab;
        }
        return null;
    }

    // ========== 局内权重偏移 API ==========

    /// <summary>
    /// 对指定品质的生成权重施加偏移（累加，可正可负）。
    /// 最终权重 = 原始权重 + 偏移，clamp 到 ≥0。
    /// 供道具效果在局内动态修改品质出现概率。
    /// </summary>
    public void ApplyQualityWeightOffset(ItemQuality quality, float delta)
    {
        EnsureInitialized();
        if (!_qualityWeightOffsets.ContainsKey(quality))
            _qualityWeightOffsets[quality] = 0f;
        _qualityWeightOffsets[quality] += delta;
    }

    /// <summary>
    /// 禁用指定品质 — 立即从可用水池移除该品质所有道具，
    /// 且本局内池空重置也不添加该品质道具。新一局 ResetPool 时清除。
    /// </summary>
    public void BanQuality(ItemQuality quality)
    {
        EnsureInitialized();
        if (_bannedQualities == null) _bannedQualities = new HashSet<ItemQuality>();
        _bannedQualities.Add(quality);

        if (_availablePool != null)
        {
            _availablePool.RemoveAll(prefab =>
            {
                if (prefab == null) return true;
                var pickup = prefab.GetComponent<ItemPickup>();
                return pickup != null && pickup.itemData != null && pickup.itemData.quality == quality;
            });
        }
    }

    // ========== 内部方法 ==========

    /// <summary>
    /// 尝试从 _availablePool 中取出指定预制体（不放回）。
    /// 检查 maxCount 过滤。不可用则返回 null。
    /// </summary>
    private GameObject TryGetSpecific(GameObject prefab, ItemManager filter)
    {
        if (prefab == null || _availablePool == null || !_availablePool.Contains(prefab))
            return null;

        // maxCount 过滤
        if (filter != null)
        {
            var pickup = prefab.GetComponent<ItemPickup>();
            if (pickup != null && pickup.itemData != null)
            {
                int maxCount = pickup.itemData.maxCount;
                if (maxCount > 0 && filter.GetItemCount(pickup.itemData.itemId) >= maxCount)
                    return null;
            }
        }

        _availablePool.Remove(prefab);
        return prefab;
    }

    /// <summary>
    /// 品质加权随机抽取一个道具（不放回），处理池空重置。
    /// </summary>
    private GameObject WeightedRandomDraw(QualityWeight[] weights, ItemManager filter)
    {
        var candidates = ResolveCandidates(filter);

        // 池空 → 重建（保留禁品质和偏移）
        if (candidates == null || candidates.Count == 0)
        {
            RebuildPool();
            candidates = ResolveCandidates(filter);
            if (candidates == null || candidates.Count == 0) return null;
        }

        var resolvedWeights = weights ?? _defaultQualityWeights;
        var picked = WeightedRandomPick(candidates, resolvedWeights);
        if (picked != null)
            _availablePool.Remove(picked);
        return picked;
    }

    /// <summary>
    /// 计算当前可用候选列表（全池 + maxCount 过滤）。
    /// </summary>
    private List<GameObject> ResolveCandidates(ItemManager filter)
    {
        if (_availablePool == null || _availablePool.Count == 0) return null;

        var candidates = new List<GameObject>(_availablePool);

        if (filter != null)
            candidates = ItemPoolFilter.GetAvailablePool(candidates, filter);

        return candidates;
    }

    /// <summary>
    /// 品质加权随机选取（不修改池，仅选取）：
    /// 1. 按品质分组
    /// 2. 仅考虑有道具的品质，按权重归一化随机出品质
    /// 3. 从该品质组中等概率随机选一个
    /// </summary>
    private GameObject WeightedRandomPick(IList<GameObject> candidates, IReadOnlyList<QualityWeight> weights)
    {
        // 按品质分组
        var groups = new Dictionary<ItemQuality, List<GameObject>>();
        foreach (var prefab in candidates)
        {
            if (prefab == null) continue;
            var pickup = prefab.GetComponent<ItemPickup>();
            if (pickup == null || pickup.itemData == null) continue;
            var q = pickup.itemData.quality;
            if (!groups.ContainsKey(q))
                groups[q] = new List<GameObject>();
            groups[q].Add(prefab);
        }

        if (groups.Count == 0) return null;

        // 构建有效品质权重表（仅含有道具的品质）
        var validQualities = new List<ItemQuality>();
        var validWeights = new List<float>();
        float totalWeight = 0f;

        foreach (var w in weights)
        {
            if (w.weight <= 0f) continue;
            if (!groups.ContainsKey(w.quality) || groups[w.quality].Count == 0) continue;

            // 应用局内权重偏移
            float finalWeight = w.weight;
            if (_qualityWeightOffsets != null && _qualityWeightOffsets.TryGetValue(w.quality, out var offset))
                finalWeight = Mathf.Max(0f, w.weight + offset);

            if (finalWeight <= 0f) continue; // 偏移后权重归零 → 跳过该品质

            validQualities.Add(w.quality);
            validWeights.Add(finalWeight);
            totalWeight += finalWeight;
        }

        // 所有配置权重对应的品质都无道具 → 从所有有道具的品质中等概率选
        if (totalWeight <= 0f)
        {
            var allKeys = new List<ItemQuality>(groups.Keys);
            var q = allKeys[Random.Range(0, allKeys.Count)];
            var g = groups[q];
            return g[Random.Range(0, g.Count)];
        }

        // 按权重随机出品质
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        ItemQuality rolledQuality = validQualities[0];

        for (int i = 0; i < validQualities.Count; i++)
        {
            cumulative += validWeights[i];
            if (roll <= cumulative)
            {
                rolledQuality = validQualities[i];
                break;
            }
        }

        // 从该品质组中等概率随机选一个
        var group = groups[rolledQuality];
        return group[Random.Range(0, group.Count)];
    }
}
