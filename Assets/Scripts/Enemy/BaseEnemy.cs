using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseEnemy : MonoBehaviour, IDamageable, ISkillCaster
{
    [Header("基础属性")]
    [SerializeField] protected float health = 30f;
    [SerializeField] protected float MaxHealth = 30f;
    [SerializeField] protected float moveSpeed = 2f;
    [SerializeField] protected float detectionRange = 5f;
    [SerializeField] protected float colliderRadius = 0.35f;
    [SerializeField] protected float attackStrength = 10f;

    [Header("技能配置")]
    [SerializeField] protected SkillLibrary _skillLibrary;        // 通用技能库 (回退)
    [SerializeField] protected SkillLibrary _enemySkillLibrary;    // 敌人专用技能库 (优先)
    [SerializeField] protected List<SkillData> _skillDataList;     // 手动指定技能列表

    [Header("技能随机抽取")]
    [Tooltip("如果 _skillDataList 为空，从此数量范围随机从技能库中抽取技能")]
    [SerializeField] private int _randomSkillMinCount = 1;
    [SerializeField] private int _randomSkillMaxCount = 2;

    protected Rigidbody2D rb;
    protected Transform playerTarget;
    protected bool isDead = false;

    protected float lastHitTime = -10f;

    /// <summary>敌人持有的技能实例（运行时创建）</summary>
    protected List<SkillInstance> _skillInstances = new();

    /// <summary>技能实例列表（供外部读取，如侵蚀技能弹窗）</summary>
    public IReadOnlyList<SkillInstance> SkillInstances => _skillInstances;

    /// <summary>敌人死亡回调</summary>
    public event System.Action OnEnemyDied;

    // ---------- ISkillCaster ----------
    public Transform CasterTransform => transform;

    public virtual Vector2 GetTargetDirection()
    {
        if (playerTarget == null) return Vector2.down;
        return (playerTarget.position - transform.position).normalized;
    }

    public float GetAttackStrength() => attackStrength;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null) colliderRadius = circle.radius * transform.localScale.x;

        _skillInstances.Clear();

        // 优先使用敌人专用技能库
        var activeLibrary = _enemySkillLibrary != null ? _enemySkillLibrary : _skillLibrary;

        if (activeLibrary != null)
        {
            // 优先使用预配置的技能列表
            var skillsToCreate = new List<SkillData>();
            if (_skillDataList != null && _skillDataList.Count > 0)
            {
                skillsToCreate.AddRange(_skillDataList);
            }
            else
            {
                // 从技能库随机抽取
                int count = Random.Range(_randomSkillMinCount, _randomSkillMaxCount + 1);
                skillsToCreate.AddRange(activeLibrary.GetRandomDistinct(count));
            }

            foreach (var data in skillsToCreate)
            {
                if (data == null) continue;
                SkillInstance instance = activeLibrary.CreateSkillInstance(data.skillId);
                if (instance != null)
                    _skillInstances.Add(instance);
            }

            if (_skillInstances.Count > 0)
                Debug.Log($"{name}: 装配了 {_skillInstances.Count} 个技能");
            else
                Debug.LogWarning($"{name}: 未能装配任何技能");
        }
    }

    protected void TryCastSkill(int index)
    {
        if (index < 0 || index >= _skillInstances.Count) return;
        _skillInstances[index].TryCast(this, GetTargetDirection());
    }

    protected void TickSkillCooldowns(float deltaTime)
    {
        foreach (var skill in _skillInstances)
        {
            skill.TickCooldown(deltaTime);
        }
    }

    // ========== 简化移动逻辑 (不再依赖 TileManager/A*) ==========

    /// <summary>朝玩家移动：视线通畅→直追，被遮挡→原地待命</summary>
    protected void MoveTowardsPlayer()
    {
        if (isDead || playerTarget == null) return;

        float distance = Vector2.Distance(transform.position, playerTarget.position);
        if (distance > detectionRange)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 使用 Physics2D.Linecast 做简单的视线检测
        Vector2 dir = (playerTarget.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Linecast(transform.position, playerTarget.position);

        if (hit.collider != null && hit.collider.CompareTag("Wall"))
        {
            // 被墙壁遮挡，停止移动
            rb.velocity = Vector2.zero;
        }
        else
        {
            // 有视线，直接移动
            rb.velocity = dir * moveSpeed;
        }
    }

    // ========== 通用移动 ==========

    private void MoveDirectlyTowards(Vector3 target)
    {
        Vector2 direction = ((Vector2)(target - transform.position)).normalized;
        rb.velocity = direction * moveSpeed;
    }

    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;
        health -= damage;
        if (health <= 0) Die();
        else StartCoroutine(FlashWhite());
    }

    protected virtual void Die()
    {
        isDead = true;
        rb.velocity = Vector2.zero;
        OnEnemyDied?.Invoke();
        Destroy(gameObject, 0.5f);
    }

    private IEnumerator FlashWhite()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) { Color c = sr.color; sr.color = Color.white; yield return new WaitForSeconds(0.1f); sr.color = c; }
    }

    protected abstract void FixedUpdate();
}
