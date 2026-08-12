using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家所有数值属性的唯一数据源。
/// PlayerController 和 PlayerCombat 通过引用读取所需属性。
/// </summary>
public class PlayerStats : MonoBehaviour, IDamageable, IHealable
{
    [Header("生命属性")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float health = 100f;

    [Header("移动属性")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("攻击属性")]
    [SerializeField] private float attackStrength = 10f;

    [Header("射击属性")]
    [SerializeField] private float bulletSpeed = 10f;
    [Tooltip("每分钟射击次数，300 = 每秒5发")]
    [SerializeField] private float shotsPerMinute = 300f;

    [Header("攻击乘区")]
    [Tooltip("攻击力乘区，真实攻击 = 基础攻击 × 此值")]
    [SerializeField] private float attackStrengthMultiplier = 1f;

    [Header("碰撞属性")]
    [SerializeField] private float colliderRadius = 0.4f;

    [Header("进化倾向")]
    [Tooltip("进化倾向：正值=朝向宿主，负值=朝向独特。范围 -100 ~ 100")]
    [SerializeField] private float evolutionTendency = 0f;

    [Header("进化倾向倍率")]
    [Tooltip("玩家技能伤害修正 = 倾向值 × 此值（叠加在技能伤害修正乘区上）")]
    [SerializeField] private float _evolveSkillDamageFactor = 0.025f;
    [Tooltip("随从全属性增幅 = 随从自身属性 × (-倾向值) × 此值")]
    [SerializeField] private float _evolveFollowerBuffFactor = 0.01f;

    private bool _isDead = false;

    /// <summary>是否免疫碰撞伤害（道具效果）</summary>
    public bool ImmuneToContactDamage { get; set; }

    // ---------- 只读属性 ----------
    public float CurrentHealth => health;
    public float MaxHealth => maxHealth>=1 ? maxHealth : 1;
    public bool IsDead => _isDead;
    public float MoveSpeed => moveSpeed;
    public float AttackStrength => (attackStrength * attackStrengthMultiplier) >= 1 ? attackStrength * attackStrengthMultiplier : 1;
    public float BaseAttackStrength => attackStrength;
    public float AttackStrengthMultiplier => attackStrengthMultiplier;
    public float BulletSpeed => bulletSpeed;
    public float ShootCooldown => 60f / shotsPerMinute;
    public float ShotsPerMinute => shotsPerMinute;
    public float ColliderRadius
    {
        get => colliderRadius;
        set => colliderRadius = Mathf.Max(value, 0.01f);
    }

    /// <summary>进化倾向：正值=朝向宿主（金色），负值=朝向独特（紫色）。范围 -100 ~ 100</summary>
    public float EvolutionTendency => evolutionTendency;
    public float EvolveSkillDamageFactor => _evolveSkillDamageFactor;
    public float EvolveFollowerBuffFactor => _evolveFollowerBuffFactor;

    // ---------- 委托 ----------
    /// <summary>生命变化委托：参数为 (当前生命, 最大生命)</summary>
    public event System.Action<float, float> OnHealthChanged;
    /// <summary>死亡委托</summary>
    public event System.Action OnDied;
    /// <summary>属性变化委托：参数为 (属性名)</summary>
    public event System.Action<string> OnStatChanged;

    private void Awake()
    {
        health = maxHealth;
    }

    // ---------- IDamageable ----------
    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        // 无敌帧：免疫期间不受到任何伤害
        var immunity = GetComponent<DamageImmunity>();
        if (immunity != null && immunity.IsImmune) return;

        // 镇痛阻滞：伤害拆分为立即 + 延迟（延迟部分记入池，buff 结束后缓慢扣除）
        var analgesic = GetComponent<AnalgesicBlockRuntime>();
        if (analgesic != null && analgesic.IsActive)
            damage = analgesic.SplitDamage(damage);

        health -= damage;
        health = Mathf.Max(health, 0);

        // 弹出伤害数字（玩家受伤用红色）
        DamagePopup.Spawn(transform.position, damage, isPlayerDamage: true);

        // 角色受击 → 屏幕振动 + 全屏红闪
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(1f);
            CameraShake.Instance.FlashRed();
        }

#if UNITY_EDITOR
        Debug.Log($"玩家受击！剩余生命：{health}");
#endif

        OnHealthChanged?.Invoke(health, maxHealth);
        OnStatChanged?.Invoke("Health");

        if (health <= 0f)
        {
            Die();
        }
    }

    // ---------- IHealable ----------
    public void Heal(float amount)
    {
        if (_isDead) return;

        health = Mathf.Min(health + amount, maxHealth);

#if UNITY_EDITOR
        Debug.Log($"玩家治疗！当前生命：{health}");
#endif

        OnHealthChanged?.Invoke(health, maxHealth);
        OnStatChanged?.Invoke("Health");
    }

    private void Die()
    {
        _isDead = true;
        Debug.Log("玩家死亡");

        OnDied?.Invoke();
    }

    // ---------- Buff 系统属性读写接口 ----------

    /// <summary>通过属性名字符串获取当前值（供 Buff 系统使用）</summary>
    public float GetStatValue(string statName)
    {
        return statName switch
        {
            "MaxHealth"               => maxHealth,
            "Health"                  => health,
            "MoveSpeed"               => moveSpeed,
            "AttackStrength"          => attackStrength,
            "AttackStrengthMultiplier" => attackStrengthMultiplier,
            "BulletSpeed"             => bulletSpeed,
            "ShotsPerMinute"          => shotsPerMinute,
            "ColliderRadius"          => colliderRadius,
            "EvolutionTendency"       => evolutionTendency,
            _                         => throw new System.ArgumentException($"PlayerStats: 未知属性名 '{statName}'")
        };
    }

    /// <summary>通过属性名字符串设置值，自动触发 OnStatChanged（供 Buff 系统使用）</summary>
    public void SetStatValue(string statName, float value)
    {
        switch (statName)
        {
            case "MaxHealth":
                maxHealth = Mathf.Max(value, 1f);
                health = Mathf.Min(health, maxHealth);
                break;
            case "Health":
                health = Mathf.Clamp(value, 0f, maxHealth);
                break;
            case "MoveSpeed":
                moveSpeed = Mathf.Max(value, 0f);
                break;
            case "AttackStrength":
                attackStrength = Mathf.Max(value, 0f);
                break;
            case "AttackStrengthMultiplier":
                attackStrengthMultiplier = Mathf.Max(value, 0f);
                break;
            case "BulletSpeed":
                bulletSpeed = Mathf.Max(value, 0f);
                break;
            case "ShotsPerMinute":
                shotsPerMinute = Mathf.Max(value, 0f);
                break;
            case "ColliderRadius":
                colliderRadius = Mathf.Max(value, 0.01f);
                break;
            case "EvolutionTendency":
                evolutionTendency = Mathf.Clamp(value, -100f, 100f);
                EnemyFollower.RefreshAllFollowers(evolutionTendency, _evolveFollowerBuffFactor);
                break;
            default:
                throw new System.ArgumentException($"PlayerStats: 未知属性名 '{statName}'");
        }
        OnStatChanged?.Invoke(statName);
    }

    // ---------- 科技树 / 配置接口 ----------

    /// <summary>应用基础配置值（来自 PlayerConfig SO）</summary>
    public void ApplyBaseStats(PlayerConfig config)
    {
        if (config == null) return;

        maxHealth = config.maxHealth;
        moveSpeed = config.moveSpeed;
        attackStrength = config.attackStrength;
        bulletSpeed = config.bulletSpeed;
        shotsPerMinute = config.shotsPerMinute;
        colliderRadius = config.colliderRadius;
        health = maxHealth; // 重置满血

        OnHealthChanged?.Invoke(health, maxHealth);
        OnStatChanged?.Invoke("All");
    }

    /// <summary>叠加科技树加成（在 ApplyBaseStats 之后调用）</summary>
    public void ApplyBonuses(Dictionary<string, float> bonuses)
    {
        if (bonuses == null || bonuses.Count == 0) return;

        foreach (var kvp in bonuses)
        {
            SetStatValue(kvp.Key, GetStatValue(kvp.Key) + kvp.Value);
        }

        // 加成后恢复满血（生命上限可能增加了）
        if (bonuses.ContainsKey("MaxHealth"))
            health = maxHealth;

        OnHealthChanged?.Invoke(health, maxHealth);
        OnStatChanged?.Invoke("All");
    }

    // ---------- 数值修正 ----------
    private void OnValidate()
    {
        health = Mathf.Clamp(health, 0, maxHealth);
        maxHealth = Mathf.Max(maxHealth, 1f);
        moveSpeed = Mathf.Max(moveSpeed, 0);
        attackStrength = Mathf.Max(attackStrength, 0);
        attackStrengthMultiplier = Mathf.Max(attackStrengthMultiplier, 0f);
        bulletSpeed = Mathf.Max(bulletSpeed, 0f);
        shotsPerMinute = Mathf.Max(shotsPerMinute, 1f);
        colliderRadius = Mathf.Max(colliderRadius, 0.01f);
        evolutionTendency = Mathf.Clamp(evolutionTendency, -100f, 100f);
    }
}
