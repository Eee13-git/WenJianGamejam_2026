using UnityEngine;

/// <summary>
/// 通用伤害免疫组件 — 挂在角色身上，授予一段时间的无敌（免疫所有伤害）。
/// 任何伤害入口（PlayerStats / EnemyStats 的 TakeDamage）都应检查 IsImmune。
/// 无敌期间角色精灵闪烁（向白色脉动）。
/// </summary>
public class DamageImmunity : MonoBehaviour
{
    [Tooltip("闪烁频率（次/秒）")]
    [SerializeField] private float _blinkFrequency = 3f;

    [Tooltip("闪烁强度：0=不变色，1=完全变白")]
    [Range(0f, 1f)] [SerializeField] private float _blinkIntensity = 0.6f;

    private float _remainingTime;

    private SpriteRenderer _sr;
    private Color _baseColor;

    /// <summary>当前是否免疫伤害</summary>
    public bool IsImmune => _remainingTime > 0f;

    /// <summary>剩余免疫时间（秒）</summary>
    public float RemainingTime => _remainingTime;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null)
            _baseColor = _sr.color;
    }

    /// <summary>授予无敌（刷新/覆盖现有免疫时长）</summary>
    public void GrantImmunity(float duration)
    {
        _remainingTime = Mathf.Max(_remainingTime, duration);
    }

    /// <summary>
    /// 立即结束无敌并恢复精灵基础色（潜行/冲刺等结束时清除 999s 长无敌，避免持续白闪）。
    /// </summary>
    public void ClearImmunity()
    {
        _remainingTime = 0f;
        if (_sr != null)
            _sr.color = _baseColor;
    }

    private void Update()
    {
        if (_remainingTime > 0f)
        {
            _remainingTime -= Time.deltaTime;

            // 闪烁：Sin 驱动 0~1 的脉动值
            float pulse = (Mathf.Sin(Time.time * _blinkFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float t = pulse * _blinkIntensity;
            if (_sr != null)
                _sr.color = Color.Lerp(_baseColor, Color.white, t);

            // 无敌结束 → 恢复基础色
            if (_remainingTime <= 0f && _sr != null)
                _sr.color = _baseColor;
        }
    }
}
