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

    // ---------- 新增：射击设置 ----------
    [Header("射击设置")]
    [SerializeField] private GameObject bulletPrefab;      // 子弹预制体
    [SerializeField] private float bulletSpeed = 10f;     // 子弹飞行速度
    [SerializeField] private float shootCooldown = 0.2f;  // 射击冷却（秒）

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isDead = false;
    private float lastShootTime = -10f;  // 上次射击时间

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

        // 检查子弹预制体是否赋值
        if (bulletPrefab == null)
        {
            Debug.LogWarning("PlayerController: 未指定子弹预制体！");
        }
    }

    void Update()
    {
        if (isDead) return; // 死亡后不响应输入

        // ---- 移动输入 ----
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical);
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        // ---- 射击输入（鼠标左键） ----
        if (Input.GetMouseButtonDown(0) && Time.time >= lastShootTime + shootCooldown)
        {
            Shoot();
            lastShootTime = Time.time;
        }
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
            Vector2[] checkPoints = new Vector2[]
            {
                nextPos,
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
        }
        else
        {
            // 5. 逐轴滑动
            Vector3 nextPosX = currentPos + new Vector3(targetVelocity.x * Time.fixedDeltaTime, 0, 0);
            Vector3 nextPosY = currentPos + new Vector3(0, targetVelocity.y * Time.fixedDeltaTime, 0);

            bool canMoveX = true;
            bool canMoveY = true;

            if (MapManager.Instance != null)
            {
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

            Vector3 finalPos = currentPos;
            if (canMoveX) finalPos.x = nextPosX.x;
            if (canMoveY) finalPos.y = nextPosY.y;
            rb.MovePosition(finalPos);
        }
    }

    // ---------- 射击方法 ----------
    private void Shoot()
    {
        if (bulletPrefab == null) return;

        // 1. 获取鼠标在游戏世界中的位置（Z轴设为0）
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        // 2. 计算从玩家指向鼠标的方向
        Vector2 direction = (mouseWorldPos - transform.position).normalized;

        // 3. 生成子弹
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Projectile proj = bullet.GetComponent<Projectile>();
        if (proj != null)
        {
            // 使用角色的攻击力和子弹速度初始化
            proj.Initialize(direction, bulletSpeed, attackStrength, Projectile.OwnerType.Player);
        }
        else
        {
            Debug.LogWarning("子弹预制体缺少 Projectile 组件！");
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
        bulletSpeed = Mathf.Max(bulletSpeed, 0f);
        shootCooldown = Mathf.Max(shootCooldown, 0f);
    }
}