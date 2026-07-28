using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseEnemy : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] protected float health = 30f;
    [SerializeField] protected float moveSpeed = 2f;
    [SerializeField] protected float detectionRange = 5f;
    [SerializeField] protected float colliderRadius = 0.35f;

    [Header("寻路设置")]
    [SerializeField] protected float pathUpdateInterval = 0.3f; // A* 寻路更新间隔
    [SerializeField] protected float pathReachThreshold = 0.2f;  // 到达路径点的判定距离

    protected Rigidbody2D rb;
    protected Transform playerTarget;
    protected MapManager mapManager;
    protected bool isDead = false;

    // 子类可以访问的受击冷却（防止高频伤害）
    protected float lastHitTime = -10f;

    // A* 寻路状态
    private List<Vector3> currentPath;
    private int pathIndex;
    private float lastPathUpdateTime = -10f;
    private Vector3 lastKnownTargetPos; // 记录上次寻路时玩家的位置

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true; // 冻结旋转，防止碰墙打转

        mapManager = MapManager.Instance;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null) colliderRadius = circle.radius * transform.localScale.x;
    }

    // ========== 寻路移动主逻辑 ==========
    // 视线畅通 → 直线追击；视线被挡 → A* 寻路绕行
    protected void MoveTowardsPlayer()
    {
        if (isDead || playerTarget == null) return;

        float distance = Vector2.Distance(transform.position, playerTarget.position);
        if (distance > detectionRange)
        {
            currentPath = null;
            rb.velocity = Vector2.zero;
            return;
        }

        if (mapManager == null)
        {
            MoveDirectlyTowards(playerTarget.position);
            return;
        }

        Vector2Int enemyCell = mapManager.WorldToGridIndex(transform.position);
        Vector2Int playerCell = mapManager.WorldToGridIndex(playerTarget.position);

        // 视线检测：HasLineOfSight 返回 true = 视线畅通（无障碍物）
        if (mapManager.HasLineOfSight(enemyCell, playerCell, colliderRadius * 0.5f))
        {
            // 视线畅通，直接朝向角色移动
            currentPath = null;
            MoveDirectlyTowards(playerTarget.position);
        }
        else
        {
            // 视线被障碍物阻挡，使用 A* 寻路绕行
            // 仅在路径耗尽、或玩家移动超过阈值且间隔到期时重新计算路径，防止频繁重算导致方向抖动
            bool needUpdate = currentPath == null ||
                              pathIndex >= currentPath.Count ||
                              (Time.time - lastPathUpdateTime >= pathUpdateInterval &&
                               Vector3.Distance(playerTarget.position, lastKnownTargetPos) > 0.5f);

            if (needUpdate)
            {
                currentPath = mapManager.FindPath(transform.position, playerTarget.position);
                // 跳过第一个路径点（敌人当前格子中心），直接朝下一个格子移动
                pathIndex = (currentPath != null && currentPath.Count > 1) ? 1 : 0;
                lastPathUpdateTime = Time.time;
                lastKnownTargetPos = playerTarget.position;
            }

            if (currentPath != null && pathIndex < currentPath.Count)
            {
                // 沿路径点移动
                Vector3 waypoint = currentPath[pathIndex];
                MoveDirectlyTowards(waypoint);

                // 到达当前路径点，进入下一个
                if (Vector2.Distance(transform.position, waypoint) < pathReachThreshold)
                    pathIndex++;
            }
            else
            {
                // 找不到路径，尝试直接朝向角色
                MoveDirectlyTowards(playerTarget.position);
            }
        }
    }

    // ========== 通用移动方法（速度驱动，物理引擎自动处理碰撞与滑墙）==========
    private void MoveDirectlyTowards(Vector3 target)
    {
        Vector2 direction = ((Vector2)(target - transform.position)).normalized;
        rb.velocity = direction * moveSpeed;
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
