using UnityEngine;

/// <summary>
/// 玩家进化倾向动画切换 — 根据进化倾向切换待机动画档位（5档渐变）：
///   t ∈ [-100, -60)  → Tier 0: UniqueIdle（极独特，深紫）
///   t ∈ [-60, -20)   → Tier 1: UniqueMidIdle（偏独特，紫蓝渐变）
///   t ∈ [-20, 20]    → Tier 2: NormalIdle（中性，纯蓝）
///   t ∈ (20, 60]     → Tier 3: HostMidIdle（偏宿主，蓝金渐变）
///   t ∈ (60, 100]    → Tier 4: HostIdle（极宿主，金色）
/// 监听 PlayerStats.OnStatChanged("EvolutionTendency") 实时切换。
/// </summary>
public class PlayerEvolveAnimator : MonoBehaviour
{
    private Animator _animator;
    private PlayerStats _stats;

    /// <summary>极独特阈值（< 此值进入极独特形态）</summary>
    [SerializeField] private float _uniqueThreshold = -60f;
    /// <summary>偏独特阈值（< 此值进入偏独特形态）</summary>
    [SerializeField] private float _uniqueMidThreshold = -20f;
    /// <summary>偏宿主阈值（> 此值进入偏宿主形态）</summary>
    [SerializeField] private float _hostMidThreshold = 20f;
    /// <summary>极宿主阈值（> 此值进入极宿主形态）</summary>
    [SerializeField] private float _hostThreshold = 60f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _stats = GetComponent<PlayerStats>();
    }

    private void Start()
    {
        ApplyTier();
        if (_stats != null)
            _stats.OnStatChanged += OnStatChanged;
    }

    private void OnDestroy()
    {
        if (_stats != null)
            _stats.OnStatChanged -= OnStatChanged;
    }

    private void OnStatChanged(string statName)
    {
        if (statName == "EvolutionTendency")
            ApplyTier();
    }

    /// <summary>根据进化倾向计算档位并设置 Animator 参数</summary>
    public void ApplyTier()
    {
        if (_animator == null) return;

        float t = _stats != null ? _stats.EvolutionTendency : 0f;
        int tier;
        if (t < _uniqueThreshold)
            tier = 0;       // 极独特
        else if (t < _uniqueMidThreshold)
            tier = 1;       // 偏独特
        else if (t <= _hostMidThreshold)
            tier = 2;       // 中性
        else if (t <= _hostThreshold)
            tier = 3;       // 偏宿主
        else
            tier = 4;       // 极宿主

        _animator.SetInteger("EvolveTier", tier);
    }
}
