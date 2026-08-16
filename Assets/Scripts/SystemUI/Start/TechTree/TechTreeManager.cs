using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 科技树管理器 (持久化单例) — 管理科技点、节点解锁、属性加成计算。
///
/// 生命周期:
///   - 通过 [RuntimeInitializeOnLoadMethod] 自动创建，DontDestroyOnLoad
///   - 科技点和解锁状态通过 TechTreeSaveSystem 持久化（跨游戏会话保留）
///
/// 核心接口:
///   - TechPoints: 当前可用科技点
///   - GetStatBonus(statName): 获取某属性的累计加成
///   - GetAllBonuses(): 获取所有属性的加成字典（供 PlayerManager 使用）
///   - CanUnlock(nodeId) / Unlock(nodeId): 解锁逻辑
///   - AddTechPoints(amount): 结算时发放科技点
/// </summary>
public class TechTreeManager : MonoBehaviour
{
    public static TechTreeManager Instance { get; private set; }

    // ========== 配置 ==========

    private const string CONFIG_PATH = "TechTree/TechTreeConfig";

    [Header("科技树配置")]
    [SerializeField] private TechTreeConfig _config;

    // ========== 运行时状态 ==========

    /// <summary>当前可用科技点</summary>
    public int TechPoints { get; private set; }

    /// <summary>已解锁节点ID集合</summary>
    private readonly HashSet<string> _unlockedNodeIds = new();

    /// <summary>已解锁节点ID（只读视图）</summary>
    public IReadOnlyCollection<string> UnlockedNodeIds => _unlockedNodeIds;

    /// <summary>属性加成缓存: statName → 累计加成值</summary>
    private readonly Dictionary<string, float> _statBonuses = new();

    /// <summary>科技点变化事件</summary>
    public event System.Action<int> OnTechPointsChanged;

    /// <summary>节点解锁事件: 参数为节点ID</summary>
    public event System.Action<string> OnNodeUnlocked;

    // ========== 生命周期 ==========

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;
        var go = new GameObject("[TechTreeManager]");
        go.AddComponent<TechTreeManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 自动加载配置
        if (_config == null)
        {
            _config = Resources.Load<TechTreeConfig>(CONFIG_PATH);
        }

        // 加载持久化数据
        Load();
        RecalculateBonuses();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    // ========== 公开接口 ==========

    /// <summary>科技树配置</summary>
    public TechTreeConfig Config => _config;

    /// <summary>获取某属性的累计科技树加成</summary>
    public float GetStatBonus(string statName)
    {
        return _statBonuses.TryGetValue(statName, out float bonus) ? bonus : 0f;
    }

    /// <summary>获取所有属性的加成字典（供 PlayerManager 应用到 PlayerStats）</summary>
    public Dictionary<string, float> GetAllBonuses()
    {
        return new Dictionary<string, float>(_statBonuses);
    }

    /// <summary>增加科技点（结算时调用）</summary>
    public void AddTechPoints(int amount)
    {
        if (amount <= 0) return;
        TechPoints += amount;
        Save();
        OnTechPointsChanged?.Invoke(TechPoints);
    }

    /// <summary>判断节点是否已解锁</summary>
    public bool IsUnlocked(string nodeId) => _unlockedNodeIds.Contains(nodeId);

    /// <summary>判断节点是否可以解锁（点数足够 + 前置满足）</summary>
    public bool CanUnlock(string nodeId)
    {
        if (_config == null) return false;
        var node = _config.GetNodeById(nodeId);
        if (node == null) return false;
        if (_unlockedNodeIds.Contains(nodeId)) return false;
        if (TechPoints < node.Cost) return false;

        // 检查前置条件
        if (node.PreviousNodes != null)
        {
            foreach (var prereq in node.PreviousNodes)
            {
                if (prereq == null) continue;
                if (!_unlockedNodeIds.Contains(prereq.NodeId))
                    return false;
            }
        }
        return true;
    }

    /// <summary>解锁节点（扣除科技点、记录、重算加成）</summary>
    public bool Unlock(string nodeId)
    {
        if (!CanUnlock(nodeId)) return false;

        var node = _config.GetNodeById(nodeId);
        TechPoints -= node.Cost;
        _unlockedNodeIds.Add(nodeId);
        RecalculateBonuses();

        // 机制门效果：解锁后立即触发
        if (node.Effect is TechTreeMechanismEffect mechEffect)
            mechEffect.ApplyMechanism();

        // 静态字段数值效果（随从/技能微强化）
        if (node.Effect is TechTreeStatMechanismEffect statMechEffect)
            statMechEffect.ApplyMechanism();

        Save();

        OnTechPointsChanged?.Invoke(TechPoints);
        OnNodeUnlocked?.Invoke(nodeId);

        return true;
    }

    /// <summary>重置所有科技树进度（调试用）</summary>
    public void ResetAll()
    {
        TechPoints = 0;
        _unlockedNodeIds.Clear();
        _statBonuses.Clear();
        TechTreeSaveSystem.Clear();
        OnTechPointsChanged?.Invoke(TechPoints);
    }

    // ========== 内部方法 ==========

    /// <summary>重新计算所有已解锁节点的属性加成</summary>
    private void RecalculateBonuses()
    {
        _statBonuses.Clear();
        if (_config == null) return;

        foreach (var nodeId in _unlockedNodeIds)
        {
            var node = _config.GetNodeById(nodeId);
            if (node == null) continue;
            if (node.Effect == null) continue;

            node.Effect.ApplyBonus(_statBonuses);
        }
    }

    // ========== 持久化（委托 TechTreeSaveSystem）==========

    private void Save()
    {
        var data = new TechTreeSaveData
        {
            techPoints = TechPoints,
            unlockedNodeIds = new List<string>(_unlockedNodeIds)
        };
        TechTreeSaveSystem.Save(data);
    }

    private void Load()
    {
        _unlockedNodeIds.Clear();
        TechPoints = 0;

        var data = TechTreeSaveSystem.Load();
        TechPoints = data.techPoints;
        if (data.unlockedNodeIds != null)
        {
            foreach (var id in data.unlockedNodeIds)
                _unlockedNodeIds.Add(id);
        }
    }
}
