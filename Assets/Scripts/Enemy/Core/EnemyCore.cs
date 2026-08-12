using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人聚合门面：实现 IEnemy / IDamageable / ISkillCaster，自动收集子组件并负责连线。
/// 碰撞伤害直接在此处理（OnTriggerStay2D + OnCollisionStay2D）。
/// 所有数值属性来源：EnemyStats（类似 PlayerStats 的单一数据源）。
/// </summary>
[DisallowMultipleComponent]
public class EnemyCore : MonoBehaviour, IEnemy
{
    [Header("配置")]
    public EnemyConfig config;

    // 子组件
    public EnemyStats Health { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemySkillManager SkillManager { get; private set; }
    public EnemyStateMachine StateMachine { get; private set; }

    // 缓存玩家引用 + 最后已知位置（供状态机读取）
    public Transform PlayerTarget { get; set; }
    public Vector2 LastKnownPlayerPosition { get; set; }

    public Transform EnemyTransform => transform;
    public bool IsDead => Health != null && Health.IsDead;
    public bool IsAssimilated { get; private set; }
    public IReadOnlyList<SkillInstance> SkillInstances =>
        SkillManager != null ? SkillManager.SkillInstances : new List<SkillInstance>();
    public event Action OnDied;
    public event Action<IEnemy> OnAssimilated;

    /// <summary>全局静态事件 — 任意敌人死亡时触发 (EnemyCore)</summary>
    public static event Action<EnemyCore> OnAnyEnemyDied;

    // 碰撞伤害冷却
    private float _lastContactDamageTime = -10f;

    private void Awake()
    {
        Health       = GetComponent<EnemyStats>();
        Movement     = GetComponent<EnemyMovement>();
        SkillManager = GetComponent<EnemySkillManager>();
        StateMachine = GetComponent<EnemyStateMachine>();

        if (Health       == null) Health       = gameObject.AddComponent<EnemyStats>();
        if (Movement     == null) Movement     = gameObject.AddComponent<EnemyMovement>();
        if (SkillManager == null) SkillManager = gameObject.AddComponent<EnemySkillManager>();
        if (StateMachine == null) StateMachine = gameObject.AddComponent<EnemyStateMachine>();

        // ═══ 单一数据源：EnemyConfig → EnemyStats（全部属性） ═══
        if (config != null)
        {
            if (Health != null)
            {
                Health.MaxHealth             = config.maxHealth;
                Health.PatrolSpeed           = config.patrolSpeed;
                Health.ChaseSpeed            = config.chaseSpeed;
                Health.DetectionRange        = config.detectionRange;
                Health.AttackRange           = config.attackRange;
                Health.ContactDamage         = config.contactDamage;
                Health.ContactDamageCooldown = config.contactDamageCooldown;
                Health.Initialize();
            }

            if (SkillManager != null)
                SkillManager.InitializeFromLibrary(config.skillLibrary);
        }
        else if (Health != null)
        {
            Health.Initialize();
        }

        // 绑定死亡事件
        if (Health != null)
        {
            Health.OnDied += () =>
            {
                OnDied?.Invoke();
                OnAnyEnemyDied?.Invoke(this);
                if (StateMachine != null)
                    StateMachine.ChangeState(new DeadState(this));
            };
        }
    }

    private void Start()
    {
        StartCoroutine(LazyFindPlayer());

        // 启动默认状态
        if (StateMachine != null)
        {
            if (config != null && config.patrolPoints != null && config.patrolPoints.Count > 0)
                StateMachine.ChangeState(new PatrolState(this));
            else
                StateMachine.ChangeState(new IdleState(this));
        }
    }

    private System.Collections.IEnumerator LazyFindPlayer()
    {
        while (PlayerTarget == null)
        {
            if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
                PlayerTarget = PlayerManager.Instance.CurrentPlayer.transform;
            else
                PlayerTarget = GameObject.FindGameObjectWithTag("Player")?.transform;

            yield return new WaitForSeconds(0.3f);
        }
    }

    // ---------- 碰撞伤害 ----------

    private void OnTriggerStay2D(Collider2D other)
    {
        ProcessContactDamage(other.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ProcessContactDamage(collision.gameObject);
    }

    private void ProcessContactDamage(GameObject other)
    {
        if (IsDead || Health == null) return;

        string targetTag = IsAssimilated ? "Enemy" : "Player";
        if (!other.CompareTag(targetTag)) return;

        if (Time.time < _lastContactDamageTime + Health.ContactDamageCooldown) return;

        // 护盾拦截
        var shield = other.GetComponent<KeratinShieldRuntime>();
        if (shield != null && shield.IsActive)
        {
            shield.TryBlock();
            _lastContactDamageTime = Time.time;
            return;
        }

        // 碰撞免疫
        var playerStats = other.GetComponent<PlayerStats>();
        if (playerStats != null && playerStats.ImmuneToContactDamage) return;

        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(Health.ContactDamage);
            _lastContactDamageTime = Time.time;
        }
    }

    // ---------- IDamageable (门面) ----------
    public void TakeDamage(float damage)
    {
        if (Health != null)
            Health.TakeDamage(damage);
    }

    // ---------- ISkillCaster ----------
    public Transform CasterTransform => transform;

    public Vector2 GetTargetDirection()
    {
        if (PlayerTarget == null) return Vector2.down;
        return (PlayerTarget.position - transform.position).normalized;
    }

    public float GetAttackStrength()
    {
        return Health != null ? Health.ContactDamage : 10f;
    }

    public Projectile.OwnerType GetOwnerType() =>
        IsAssimilated ? Projectile.OwnerType.Player : Projectile.OwnerType.Enemy;

    public float GetSkillDamageModifier() => 1f;

    // ---------- 同化 ----------
    public void Assimilate(Transform playerTarget)
    {
        IsAssimilated = true;

        if (Health != null)
            Health.ClearOnDied();

        OnAssimilated?.Invoke(this);

        gameObject.tag = "Player";

        if (StateMachine != null)
            StateMachine.enabled = false;

        Movement?.Stop();

        EnemyFollower follower = GetComponent<EnemyFollower>();
        if (follower == null)
            follower = gameObject.AddComponent<EnemyFollower>();
        follower.Activate(playerTarget);

        PlayerTarget = playerTarget;
    }
}
