using System.Collections;
using UnityEngine;

public abstract class BaseEnemy : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] protected float health = 30f;
    [SerializeField] protected float MaxHealth = 30f;
    [SerializeField] protected float moveSpeed = 2f;
    [SerializeField] protected float detectionRange = 5f;
    [SerializeField] protected float colliderRadius = 0.35f;

    protected Rigidbody2D rb;
    protected Transform playerTarget;
    protected MapManager mapManager;
    protected bool isDead = false;

    // 子类可以访问的受击冷却（防止高频伤害）
    protected float lastHitTime = -10f;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        mapManager = MapManager.Instance;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null) colliderRadius = circle.radius * transform.localScale.x;
    }

    // 通用的移动逻辑（带障碍物滑动）
    protected void MoveTowardsPlayer()
    {
        if (isDead || playerTarget == null) return;

        Vector2 direction = (playerTarget.position - transform.position).normalized;
        Vector2 targetVelocity = direction * moveSpeed;
        Vector3 currentPos = rb.position;
        Vector3 nextPos = currentPos + (Vector3)targetVelocity * Time.fixedDeltaTime;

        // 障碍物检测（多点检测，防止卡墙）
        bool canMove = true;
        if (mapManager != null)
        {
            Vector2[] points = new Vector2[] { nextPos, nextPos + Vector3.right * colliderRadius, nextPos + Vector3.left * colliderRadius, nextPos + Vector3.up * colliderRadius, nextPos + Vector3.down * colliderRadius };
            foreach (Vector3 p in points) if (!mapManager.IsWalkable(p)) { canMove = false; break; }
        }

        if (canMove) { rb.MovePosition(nextPos); }
        else
        { /* 逐轴滑动（简化版） */
            Vector3 finalPos = currentPos;
            Vector3 nextX = currentPos + new Vector3(targetVelocity.x * Time.fixedDeltaTime, 0, 0);
            Vector3 nextY = currentPos + new Vector3(0, targetVelocity.y * Time.fixedDeltaTime, 0);
            if (mapManager == null || mapManager.IsWalkable(nextX)) finalPos.x = nextX.x;
            if (mapManager == null || mapManager.IsWalkable(nextY)) finalPos.y = nextY.y;
            rb.MovePosition(finalPos);
        }
    }

    // --- 对外公共接口（供玩家攻击调用） ---
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;
        health -= damage;
        if (health <= 0) Die();
        else StartCoroutine(FlashWhite()); // 受击闪白
    }

    protected virtual void Die()
    {
        isDead = true;
        rb.velocity = Vector2.zero;
        Destroy(gameObject, 0.5f); // 简单销毁，可换为死亡动画
    }

    private IEnumerator FlashWhite()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) { Color c = sr.color; sr.color = Color.white; yield return new WaitForSeconds(0.1f); sr.color = c; }
    }

    // 子类必须实现自己的行为逻辑
    protected abstract void FixedUpdate();
}
