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
    [SerializeField] protected SkillLibrary _skillLibrary;          // 技能库引用
    [SerializeField] protected List<SkillData> _skillDataList;      // 敌人使用的技能数据

    protected Rigidbody2D rb;
    protected Transform playerTarget;
    protected MapManager mapManager;
    protected bool isDead = false;

    protected float lastHitTime = -10f;

    /// <summary>敌人持有的技能实例（运行时创建）</summary>
    protected List<SkillInstance> _skillInstances = new();

    // ---------- ISkillCaster ----------
    public Transform CasterTransform => transform;

    /// <summary>敌人目标方向：指向玩家</summary>
    public virtual Vector2 GetTargetDirection()
    {
        if (playerTarget == null) return Vector2.down;
        return (playerTarget.position - transform.position).normalized;
    }

    public float GetAttackStrength() => attackStrength;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        mapManager = MapManager.Instance;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null) colliderRadius = circle.radius * transform.localScale.x;

        // 通过 SkillLibrary 工厂创建技能实例
        _skillInstances.Clear();
        if (_skillLibrary != null)
        {
            foreach (var data in _skillDataList)
            {
                if (data == null) continue;
                SkillInstance instance = _skillLibrary.CreateSkillInstance(data.skillId);
                if (instance != null)
                    _skillInstances.Add(instance);
            }
        }
    }

    /// <summary>AI 驱动释放技能，子类在 FixedUpdate 中调用</summary>
    protected void TryCastSkill(int index)
    {
        if (index < 0 || index >= _skillInstances.Count) return;
        _skillInstances[index].TryCast(this, GetTargetDirection());
    }

    /// <summary>驱动所有技能的冷却</summary>
    protected void TickSkillCooldowns(float deltaTime)
    {
        foreach (var skill in _skillInstances)
        {
            skill.TickCooldown(deltaTime);
        }
    }

    protected void MoveTowardsPlayer()
    {
        if (isDead || playerTarget == null) return;

        Vector2 direction = (playerTarget.position - transform.position).normalized;
        Vector2 targetVelocity = direction * moveSpeed;
        Vector3 currentPos = rb.position;
        Vector3 nextPos = currentPos + (Vector3)targetVelocity * Time.fixedDeltaTime;

        bool canMove = true;
        if (mapManager != null)
        {
            Vector2[] points = new Vector2[] { nextPos, nextPos + Vector3.right * colliderRadius, nextPos + Vector3.left * colliderRadius, nextPos + Vector3.up * colliderRadius, nextPos + Vector3.down * colliderRadius };
            foreach (Vector3 p in points) if (!mapManager.IsWalkable(p)) { canMove = false; break; }
        }

        if (canMove) { rb.MovePosition(nextPos); }
        else
        {
            Vector3 finalPos = currentPos;
            Vector3 nextX = currentPos + new Vector3(targetVelocity.x * Time.fixedDeltaTime, 0, 0);
            Vector3 nextY = currentPos + new Vector3(0, targetVelocity.y * Time.fixedDeltaTime, 0);
            if (mapManager == null || mapManager.IsWalkable(nextX)) finalPos.x = nextX.x;
            if (mapManager == null || mapManager.IsWalkable(nextY)) finalPos.y = nextY.y;
            rb.MovePosition(finalPos);
        }
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
        Destroy(gameObject, 0.5f);
    }

    private IEnumerator FlashWhite()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) { Color c = sr.color; sr.color = Color.white; yield return new WaitForSeconds(0.1f); sr.color = c; }
    }

    protected abstract void FixedUpdate();
}
