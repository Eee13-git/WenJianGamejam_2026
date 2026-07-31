using System;
using UnityEngine;

/// <summary>
/// 敌人生存组件，负责血量管理并通过委托广播变化。
/// 实现 IDamageable 以便外部统一调用。
///
/// 数据源：MaxHealth 由 EnemyCore（或外部）在 Awake 后显式赋值，
///         然后调用 Initialize() 完成初始化。不在 Awake 中自动初始化。
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("生命")]
    [Tooltip("由 EnemyCore 根据 EnemyConfig 赋值，也可独立设置")]
    public float MaxHealth = 30f;

    private float _health;
    private bool _isDead = false;
    private bool _initialized = false;

    public bool IsDead => _isDead;
    public float CurrentHealth => _health;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action<float> OnDamaged;

    /// <summary>
    /// 由 EnemyCore 在设置 MaxHealth 后调用。
    /// 确保 _health 从最终 MaxHealth 初始化，避免数据源不一致。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        _health = MaxHealth;
        _initialized = true;
    }

    private void Awake()
    {
        // 不再自动初始化，由调用方（EnemyCore）控制初始化时机
    }

    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _health -= damage;
        _health = Mathf.Max(_health, 0f);

        OnDamaged?.Invoke(damage);
        OnHealthChanged?.Invoke(_health, MaxHealth);

        if (_health <= 0f)
        {
            Die();
        }
    }

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
}
