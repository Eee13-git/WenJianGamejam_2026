using UnityEngine;

/// <summary>
/// 减伤窗口组件 — 挂在角色身上，受击后开启一段减伤窗口。
/// 供细胞膜等"受击后减伤"效果使用。
/// </summary>
public class DamageReduction : MonoBehaviour
{
    [Tooltip("减伤比例，0.3 = 减伤30%")]
    public float ReductionFactor = 0.3f;
    [Tooltip("减伤窗口持续时间（秒）")]
    public float WindowDuration = 1f;

    private float _windowRemaining;
    private PlayerStats _stats;

    private void Start()
    {
        _stats = GetComponent<PlayerStats>();
        if (_stats != null)
            _stats.OnDamaged += OnDamaged;
    }

    private void OnDestroy()
    {
        if (_stats != null)
            _stats.OnDamaged -= OnDamaged;
    }

    private void OnDamaged(float dmg)
    {
        _windowRemaining = WindowDuration;
    }

    private void Update()
    {
        if (_windowRemaining > 0f)
            _windowRemaining -= Time.deltaTime;
    }

    /// <summary>应用减伤：窗口期间返回 damage × (1 - factor)，否则原样返回</summary>
    public float Apply(float damage)
    {
        return _windowRemaining > 0f ? damage * (1f - ReductionFactor) : damage;
    }
}
