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

    [Header("动画（可选，留空则从 config displayName 自动查找）")]
    [Tooltip("AnimatorController 资产路径（Assets/ 开始），留空则按敌人名自动查找")]
    [SerializeField] private string _animatorControllerPath = "";

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

    /// <summary>全局静态事件 — 任意敌人接触伤害命中时触发 (enemy, hitTarget)</summary>
    public static event Action<EnemyCore, GameObject> OnAnyContactHit;

    /// <summary>玩家碰撞伤害减免乘区（细胞骨架），1=正常，0.7=减免30%</summary>
    public static float PlayerCollisionReductionFactor = 1f;

    // 碰撞伤害冷却
    private float _lastContactDamageTime = -10f;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

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

        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 运行时 fallback：若 Animator 缺少 controller，按敌人名自动加载
        if (_animator != null && _animator.runtimeAnimatorController == null && config != null)
        {
            string enemyName = config.displayName;
            string autoPath = "Assets/Animations/Enemy/" + enemyName + "/" + enemyName + "_Controller.controller";
#if UNITY_EDITOR
            var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(autoPath);
            if (ctrl != null)
            {
                _animator.runtimeAnimatorController = ctrl;
            }
            else
            {
                Debug.LogWarning("[EnemyCore] AnimatorController not found at: " + autoPath);
            }
#else
            // 非 Editor 环境需将 controller 放入 Resources 文件夹
            var ctrl = Resources.Load<RuntimeAnimatorController>(enemyName + "_Controller");
            if (ctrl != null)
                _animator.runtimeAnimatorController = ctrl;
#endif
        }

        // 技能释放 → 触发释放动画
        if (SkillManager != null)
        {
            SkillManager.OnSkillCast += (_, _) =>
            {
                if (_animator != null && HasAnimatorParameter("IsCasting"))
                {
                    _animator.SetBool("IsCasting", true);
                    StartCoroutine(ResetCastBool(1.4f));
                }
            };
        }

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
            {
                // 固定技能列表优先：按 ID 精确装配；否则从技能库随机抽取
                if (config.fixedSkillIds != null && config.fixedSkillIds.Count > 0)
                    SkillManager.LoadSkills(config.skillLibrary, config.fixedSkillIds.ToArray());
                else
                    SkillManager.InitializeFromLibrary(config.skillLibrary);

                // 被动技能（永久光环等）在装配后自动执行一次
                SkillManager.AutoCastPassives(this);
            }
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

    private System.Collections.IEnumerator ResetCastBool(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_animator != null && HasAnimatorParameter("IsCasting"))
            _animator.SetBool("IsCasting", false);
    }

    /// <summary>检查 Animator 是否包含指定参数（避免 SetBool 报 "Parameter does not exist"）</summary>
    private bool HasAnimatorParameter(string paramName)
    {
        if (_animator == null || _animator.parameters == null) return false;
        foreach (var p in _animator.parameters)
            if (p.name == paramName) return true;
        return false;
    }

    // ---------- 朝向 ----------
    private void LateUpdate()
    {
        if (IsDead || _spriteRenderer == null || PlayerTarget == null) return;

        // 施法期间也允许翻转，始终跟随玩家方向
        float dx = PlayerTarget.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.1f)
            _spriteRenderer.flipX = dx < 0f;
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

        float dmg = Health.ContactDamage;

        // 细胞骨架：非随从（敌人）对玩家的碰撞伤害减免
        if (!IsAssimilated && PlayerCollisionReductionFactor < 1f)
            dmg *= PlayerCollisionReductionFactor;

        // 干扰素：随从攻击命中时给目标减速
        if (IsAssimilated && EnemyFollower.ApplySlow && other.CompareTag("Enemy"))
        {
            var slow = other.GetComponent<SlowEffect>();
            if (slow == null)
                slow = other.AddComponent<SlowEffect>();
            slow.Apply(EnemyFollower.SlowFactor, EnemyFollower.SlowDuration);
        }

        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(dmg);
            _lastContactDamageTime = Time.time;

            // 广播接触命中事件（供 OnContactHitBuff 等效果使用）
            OnAnyContactHit?.Invoke(this, other.gameObject);
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
