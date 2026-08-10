using UnityEngine;

/// <summary>
/// 低血量属性增幅 Buff — 类似以撒巴比伦大淫妇。
/// 当目标血量低于阈值时，自动应用配置的属性增幅（乘区）。
/// 血量恢复超过阈值时，还原原始值。
///
/// 配置方式: 在 Inspector 中设置 healthThreshold 和 statBoosts 数组。
/// 使用场景: 作为 BuffData.effect 使用（通常配合 ApplyBuffEffect 物品）。
/// </summary>
[CreateAssetMenu(fileName = "LowHealthStatBoost", menuName = "Game/Buff Effect/Low Health Stat Boost")]
public class LowHealthStatBoost : BuffEffectBase
{
    [System.Serializable]
    public struct StatBoostEntry
    {
        [Tooltip("属性名: AttackStrengthMultiplier / ShotsPerMinuteMultiplier 等")]
        public string statName;
        [Tooltip("加成值，真实乘区 = 基础乘区 + 此值")]
        public float bonus;
    }

    [Header("触发条件")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("血量比例阈值，低于此值时触发增幅")]
    private float _healthThreshold = 0.5f;

    [Header("属性增幅")]
    [SerializeField]
    [Tooltip("低于阈值时应用的属性乘区")]
    private StatBoostEntry[] _statBoosts;

    // 运行时状态
    private bool _isBoosting;
    private PlayerStats _stats;
    private BuffInstance _hostBuff;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _hostBuff = buff;
        _stats = target?.GetComponent<PlayerStats>();
        if (_stats == null) return;

        // 重置状态：ScriptableObject 实例字段跨 Play 会话持久存在，
        // 必须在 OnApply 时重置，否则 _isBoosting 残留会导致乘除错乱
        _isBoosting = false;

        _stats.OnHealthChanged += OnHealthChanged;
        // 立即检查条件
        CheckThreshold();
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (_stats != null)
            _stats.OnHealthChanged -= OnHealthChanged;

        // 还原所有增幅
        if (_isBoosting)
        {
            ApplyBoosts(false);
            _isBoosting = false;
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        CheckThreshold();
    }

    private void CheckThreshold()
    {
        if (_stats == null) return;

        float ratio = _stats.MaxHealth > 0 ? _stats.CurrentHealth / _stats.MaxHealth : 0f;
        bool shouldBoost = ratio < _healthThreshold;

        if (shouldBoost && !_isBoosting)
        {
            ApplyBoosts(true);
            _isBoosting = true;
        }
        else if (!shouldBoost && _isBoosting)
        {
            ApplyBoosts(false);
            _isBoosting = false;
        }
    }

    private void ApplyBoosts(bool boost)
    {
        if (_statBoosts == null) return;

        foreach (var entry in _statBoosts)
        {
            if (string.IsNullOrEmpty(entry.statName)) continue;

            try
            {
                float current = _stats.GetStatValue(entry.statName);
                if (boost)
                {
                    _stats.SetStatValue(entry.statName, current + entry.bonus);
                }
                else
                {
                    _stats.SetStatValue(entry.statName, current - entry.bonus);
                }
            }
            catch (System.ArgumentException)
            {
                Debug.LogWarning($"LowHealthStatBoost: 未知属性名 '{entry.statName}'");
            }
        }
    }
}
