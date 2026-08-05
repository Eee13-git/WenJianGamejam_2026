using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人聚合门面：实现 IEnemy / IDamageable / ISkillCaster，自动收集子组件并负责连线。
/// 所有对外系统（Spawner、技能效果等）应依赖 IEnemy 接口。
/// </summary>
[DisallowMultipleComponent]
public class EnemyCore : MonoBehaviour, IEnemy
{
    [Header("配置")]
    public EnemyConfig config;

    // 子组件
    public EnemyHealth Health { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemySkillManager SkillManager { get; private set; }
    public EnemyStateMachine StateMachine { get; private set; }
    public IAttackBehavior AttackBehavior { get; private set; }

    // 缓存玩家引用 + 最后已知位置（供状态机读取）
    public Transform PlayerTarget { get; set; }
    public Vector2 LastKnownPlayerPosition { get; set; }

    public Transform EnemyTransform => transform;
    public bool IsDead => Health != null && Health.IsDead;
    public bool IsAssimilated { get; private set; }
    public IReadOnlyList<SkillInstance> SkillInstances => SkillManager != null ? SkillManager.SkillInstances : new List<SkillInstance>();
    public event Action OnDied;
    public event Action<IEnemy> OnAssimilated;

    /// <summary>全局静态事件 — 任意敌人死亡时触发 (EnemyCore)</summary>
    public static event Action<EnemyCore> OnAnyEnemyDied;

    private void Awake()
    {
        Health = GetComponent<EnemyHealth>();
        Movement = GetComponent<EnemyMovement>();
        SkillManager = GetComponent<EnemySkillManager>();
        StateMachine = GetComponent<EnemyStateMachine>();
        AttackBehavior = GetComponent<IAttackBehavior>();

        if (Health == null) Health = gameObject.AddComponent<EnemyHealth>();
        if (Movement == null) Movement = gameObject.AddComponent<EnemyMovement>();
        if (SkillManager == null) SkillManager = gameObject.AddComponent<EnemySkillManager>();
        if (StateMachine == null) StateMachine = gameObject.AddComponent<EnemyStateMachine>();

        // ═══ 单一数据源：EnemyConfig → EnemyCore → 各组件 ═══
        if (config != null)
        {
            if (Health != null)
            {
                Health.MaxHealth = config.maxHealth;
                Health.Initialize();
            }

            if (Movement != null)
                Movement.MoveSpeed = config.patrolSpeed;

            if (SkillManager != null)
                SkillManager.InitializeFromLibrary(config.skillLibrary);
        }
        else if (Health != null)
        {
            // 无 config 时，允许组件自身的 MaxHealth 作为默认值
            Health.Initialize();
        }

        // 绑定死亡事件
        if (Health != null)
        {
            Health.OnDied += () =>
            {
                OnDied?.Invoke();
                OnAnyEnemyDied?.Invoke(this);
                // 切换到死亡状态
                if (StateMachine != null)
                    StateMachine.ChangeState(new DeadState(this));
            };
        }
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) PlayerTarget = player.transform;

        // 启动默认状态（若存在巡逻点则 Patrol，否则 Idle）
        if (StateMachine != null)
        {
            if (config != null && config.patrolPoints != null && config.patrolPoints.Count > 0)
                StateMachine.ChangeState(new PatrolState(this));
            else
                StateMachine.ChangeState(new IdleState(this));
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
        // 优先从 MeleeAttack 组件读取伤害值
        if (AttackBehavior is MeleeAttack melee)
            return melee.damage;
        if (AttackBehavior is RangedAttack ranged)
            return ranged.damage;
        return 10f;
    }

    /// <summary>
    /// 施法者阵营：被同化后为 Player（技能命中 Enemy），否则为 Enemy（技能命中 Player）。
    /// </summary>
    public Projectile.OwnerType GetOwnerType() =>
        IsAssimilated ? Projectile.OwnerType.Player : Projectile.OwnerType.Enemy;

    // ---------- 同化 ----------
    /// <summary>
    /// 将被侵蚀的敌人转化为玩家的随从。
    /// - 改 tag 为 Player（让其他敌人的攻击能命中此随从）
    /// - 禁用原状态机，由 EnemyFollower 接管 AI
    /// - 清除旧的死亡事件，由 EnemyFollower 处理死亡
    /// </summary>
    public void Assimilate(Transform playerTarget)
    {
        // 1. 标记已同化（房间/生成器通过 OnAssimilated 事件从存活列表移除）
        IsAssimilated = true;

        // 2. 清除旧的死亡订阅（由 EnemyFollower 接管死亡处理）
        if (Health != null)
            Health.ClearOnDied();

        // 3. 通知外部系统：此敌人不再是敌人
        OnAssimilated?.Invoke(this);

        // 4. 改 tag，使敌人攻击能命中此随从
        gameObject.tag = "Player";

        // 5. 禁用原有状态机
        if (StateMachine != null)
            StateMachine.enabled = false;

        // 6. 停止当前移动
        Movement?.Stop();

        // 7. 添加并激活随从组件
        EnemyFollower follower = GetComponent<EnemyFollower>();
        if (follower == null)
            follower = gameObject.AddComponent<EnemyFollower>();
        follower.Activate(playerTarget);

        // 8. 更新玩家引用（供技能方向等使用）
        PlayerTarget = playerTarget;
    }
}
