using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("角色属性")]
    [SerializeField] private float health = 100f;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float attackStrength = 10f;

    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;

    // 碰撞体半径（用于检测时考虑角色大小）
    [Header("碰撞检测")]
    [SerializeField] private float colliderRadius = 0.4f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isDead = false;  // 新增死亡状态

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning("PlayerController: 未找到 Rigidbody2D 组件！");
        }

        // 自动从 CircleCollider2D 读取半径（如果有）
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            colliderRadius = circle.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        }
    }

    void Update()
    {
        if (isDead) return; // 死亡后不响应输入

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical);
        // 保留手柄轻推手感，同时防止键盘斜向超速
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    void FixedUpdate()
    {
        if (rb == null || isDead) return;

        // 1. 计算期望速度
        Vector2 targetVelocity = moveInput * moveSpeed;

        // 2. 计算下一帧的位置（世界坐标）
        Vector3 currentPos = rb.position;
        Vector3 nextPos = currentPos + (Vector3)targetVelocity * Time.fixedDeltaTime;

        // 3. 使用 MapManager 检测目标位置是否可通行
        bool canMove = true;
        if (MapManager.Instance != null)
        {
            // 检测时考虑角色半径：检测目标位置的圆形范围内是否有障碍物
            // 方法：在目标位置周围取多个点检测（简单起见，我们只检测中心点和4个方向偏移点）
            Vector2[] checkPoints = new Vector2[]
            {
                nextPos, // 中心
                nextPos + Vector3.right * colliderRadius,
                nextPos + Vector3.left * colliderRadius,
                nextPos + Vector3.up * colliderRadius,
                nextPos + Vector3.down * colliderRadius
            };

            foreach (Vector3 point in checkPoints)
            {
                if (!MapManager.Instance.IsWalkable(point))
                {
                    canMove = false;
                    break;
                }
            }
        }

        // 4. 如果目标位置可通行，直接移动
        if (canMove)
        {
            rb.MovePosition(nextPos);
            // 或者用 rb.velocity = targetVelocity;（但 MovePosition 更平滑）
            // 这里推荐使用 MovePosition 避免物理穿透
        }
        else
        {
            // 5. 如果不可通行，尝试“逐轴滑动”：
            // 先尝试单独沿 X 轴移动，再尝试单独沿 Y 轴
            Vector3 nextPosX = currentPos + new Vector3(targetVelocity.x * Time.fixedDeltaTime, 0, 0);
            Vector3 nextPosY = currentPos + new Vector3(0, targetVelocity.y * Time.fixedDeltaTime, 0);

            bool canMoveX = true;
            bool canMoveY = true;

            if (MapManager.Instance != null)
            {
                // 检查 X 轴方向（同样考虑半径）
                Vector2[] checkPointsX = new Vector2[]
                {
                    nextPosX,
                    nextPosX + Vector3.right * colliderRadius,
                    nextPosX + Vector3.left * colliderRadius,
                    nextPosX + Vector3.up * colliderRadius,
                    nextPosX + Vector3.down * colliderRadius
                };
                foreach (Vector3 point in checkPointsX)
                {
                    if (!MapManager.Instance.IsWalkable(point)) { canMoveX = false; break; }
                }

                // 检查 Y 轴方向
                Vector2[] checkPointsY = new Vector2[]
                {
                    nextPosY,
                    nextPosY + Vector3.right * colliderRadius,
                    nextPosY + Vector3.left * colliderRadius,
                    nextPosY + Vector3.up * colliderRadius,
                    nextPosY + Vector3.down * colliderRadius
                };
                foreach (Vector3 point in checkPointsY)
                {
                    if (!MapManager.Instance.IsWalkable(point)) { canMoveY = false; break; }
                }
            }

            // 分别应用可移动的轴
            Vector3 finalPos = currentPos;
            if (canMoveX) finalPos.x = nextPosX.x;
            if (canMoveY) finalPos.y = nextPosY.y;

            rb.MovePosition(finalPos);
        }
    }

    // ---------- 对外接口 ----------
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        health -= damage;
        health = Mathf.Max(health, 0);

#if UNITY_EDITOR
        Debug.Log($"玩家受伤，剩余生命：{health}");
#endif

        if (health <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        health = Mathf.Min(health + amount, maxHealth);
    }

    private void Die()
    {
        isDead = true;
        rb.velocity = Vector2.zero;
        Debug.Log("玩家死亡");
        // 可触发 UI 或事件
    }

    public float GetHealth() => health;
    public float GetMaxHealth() => maxHealth;
    public float GetAttackStrength() => attackStrength;
    public bool IsDead() => isDead;

    // 编辑器下自动限制数值
    private void OnValidate()
    {
        health = Mathf.Clamp(health, 0, maxHealth);
        attackStrength = Mathf.Max(attackStrength, 0);
        moveSpeed = Mathf.Max(moveSpeed, 0);
        colliderRadius = Mathf.Max(colliderRadius, 0.01f);
    }
}