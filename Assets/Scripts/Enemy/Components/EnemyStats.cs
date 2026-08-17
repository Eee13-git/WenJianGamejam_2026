using System;
using UnityEngine;

/// <summary>
/// 敌人统计数据组件 — 所有属性的唯一数据源（类似 PlayerStats）。
/// 血量、移速、感知范围、攻击范围、碰撞伤害等统一在此管理，
/// 提供 GetStatValue / SetStatValue 接口供 Buff 系统读写。
///
/// 数据初始化：EnemyCore 在 Awake 中从 EnemyConfig 赋值，然后调用 Initialize()。
/// </summary>
public class EnemyStats : MonoBehaviour, IDamageable, IHealable
{
    // ════════════════════════════════════════════
    //  生命
    // ════════════════════════════════════════════
    [Header("──── 生命 ────")]
    public float MaxHealth = 30f;
    private float _health;
    private bool _isDead = false;
    private bool _initialized = false;

    // ════════════════════════════════════════════
    //  移动速度
    // ════════════════════════════════════════════
    [Header("──── 移动速度 ────")]
    [Tooltip("巡逻速度")]
    public float PatrolSpeed = 1f;
    [Tooltip("追击速度")]
    public float ChaseSpeed = 2.5f;

    // ════════════════════════════════════════════
    //  感知与攻击
    // ════════════════════════════════════════════
    [Header("──── 感知与攻击 ────")]
    [Tooltip("发现玩家范围")]
    public float DetectionRange = 5f;
    [Tooltip("攻击范围")]
    public float AttackRange = 1.2f;

    // ════════════════════════════════════════════
    //  碰撞伤害
    // ════════════════════════════════════════════
    [Header("──── 碰撞伤害 ────")]
    [Tooltip("接触玩家时造成的伤害")]
    public float ContactDamage = 10f;
    [Tooltip("碰撞伤害冷却（秒）")]
    public float ContactDamageCooldown = 1f;

    // ──────────────────────────────────────────
    //  只读属性
    // ──────────────────────────────────────────
    public bool IsDead => _isDead;
    public float CurrentHealth => _health;

    // ──────────────────────────────────────────
    //  事件
    // ──────────────────────────────────────────
    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action<float> OnDamaged;
    /// <summary>属性变化事件：参数为 (属性名)</summary>
    public event Action<string> OnStatChanged;

    /// <summary>技能命中附带目标当前生命的百分比伤害（蛋白激酶），0=关闭</summary>
    public static float OnSkillHitPercentDamage = 0f;

    // ──────────────────────────────────────────
    //  初始化
    // ──────────────────────────────────────────

    /// <summary>
    /// 由 EnemyCore 在设置各项属性后调用。
    /// 确保 _health 从最终 MaxHealth 初始化。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _health = MaxHealth;
        _initialized = true;
    }

    // ──────────────────────────────────────────
    //  IDamageable
    // ──────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        var immunity = GetComponent<DamageImmunity>();
        if (immunity != null && immunity.IsImmune) return;

        // 受击闪避（肌纤维应激细胞等）：成功闪避则免疫本次伤害并滑移
        var dodge = GetComponent<DodgeComponent>();
        if (dodge != null)
        {
            // 闪避方向：远离最近的敌人（通常是玩家/攻击者方向的反向）
            Vector2 hitDir = Vector2.right;
            var player = PlayerManager.Instance != null ? PlayerManager.Instance.CurrentPlayer : null;
            if (player != null)
                hitDir = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;
            if (hitDir.sqrMagnitude < 0.01f)
                hitDir = Vector2.right;

            if (dodge.TryDodge(hitDir))
                return;  // 闪避成功，免疫本次伤害
        }

        // 伤害吸收护盾（角质增厚细胞等）：先免伤再吸收
        var absorbShield = GetComponent<AbsorbShield>();
        if (absorbShield != null && absorbShield.IsActive)
            absorbShield.TryAbsorb(ref damage);

        if (damage <= 0f) return;

        var analgesic = GetComponent<AnalgesicBlockRuntime>();
        if (analgesic != null && analgesic.IsActive)
            damage = analgesic.SplitDamage(damage);

        // 蛋白激酶：技能命中附带目标当前生命百分比伤害
        if (SkillInstance.SkillDamageActive && OnSkillHitPercentDamage > 0f)
            damage += _health * OnSkillHitPercentDamage;

        _health -= damage;
        _health = Mathf.Max(_health, 0f);

        DamagePopup.Spawn(transform.position, damage);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.7f);

        OnDamaged?.Invoke(damage);
        OnHealthChanged?.Invoke(_health, MaxHealth);

        if (_health <= 0f)
            Die();
    }

    // ──────────────────────────────────────────
    //  IHealable
    // ──────────────────────────────────────────

    public void Heal(float amount)
    {
        if (_isDead) return;
        _health = Mathf.Min(_health + amount, MaxHealth);
        OnHealthChanged?.Invoke(_health, MaxHealth);
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;
        OnDied?.Invoke();
    }

    /// <summary>清除 OnDied 事件的所有订阅者（同化时切换死亡处理）。</summary>
    public void ClearOnDied()
    {
        OnDied = null;
    }

    // ──────────────────────────────────────────
    //  Buff 系统属性读写接口（类同 PlayerStats）
    // ──────────────────────────────────────────

    public float GetStatValue(string statName)
    {
        return statName switch
        {
            "MaxHealth"             => MaxHealth,
            "Health"                => _health,
            "PatrolSpeed"           => PatrolSpeed,
            "ChaseSpeed"            => ChaseSpeed,
            "DetectionRange"        => DetectionRange,
            "AttackRange"           => AttackRange,
            "ContactDamage"         => ContactDamage,
            "ContactDamageCooldown" => ContactDamageCooldown,
            _ => throw new ArgumentException($"EnemyStats: 未知属性名 '{statName}'")
        };
    }

    public void SetStatValue(string statName, float value)
    {
        switch (statName)
        {
            case "MaxHealth":
                MaxHealth = Mathf.Max(value, 1f);
                _health = Mathf.Min(_health, MaxHealth);
                break;
            case "Health":
                _health = Mathf.Clamp(value, 0f, MaxHealth);
                break;
            case "PatrolSpeed":
                PatrolSpeed = Mathf.Max(value, 0f);
                break;
            case "ChaseSpeed":
                ChaseSpeed = Mathf.Max(value, 0f);
                break;
            case "DetectionRange":
                DetectionRange = Mathf.Max(value, 0f);
                break;
            case "AttackRange":
                AttackRange = Mathf.Max(value, 0f);
                break;
            case "ContactDamage":
                ContactDamage = Mathf.Max(value, 0f);
                break;
            case "ContactDamageCooldown":
                ContactDamageCooldown = Mathf.Max(value, 0f);
                break;
            default:
                throw new ArgumentException($"EnemyStats: 未知属性名 '{statName}'");
        }
        OnStatChanged?.Invoke(statName);
    }
}
