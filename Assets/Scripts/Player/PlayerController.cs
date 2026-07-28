using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D _rb;
    private PlayerCombat _combat;
    private PlayerStats _stats;
    private Vector2 _moveInput;

    /// <summary>攻击输入委托，由 PlayerCombat 订阅</summary>
    public event System.Action OnAttackInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _combat = GetComponent<PlayerCombat>();
        _stats = GetComponent<PlayerStats>();

#if UNITY_EDITOR
        if (_rb == null)
            Debug.LogWarning("PlayerController: 未找到 Rigidbody2D 组件！");
        if (_combat == null)
            Debug.LogWarning("PlayerController: 未找到 PlayerCombat 组件！");
        if (_stats == null)
            Debug.LogWarning("PlayerController: 未找到 PlayerStats 组件！");
#endif

        // 委托绑定：攻击输入 -> PlayerCombat.TryShoot
        if (_combat != null)
            OnAttackInput += _combat.TryShoot;
    }

    void Start()
    {
        if (_stats == null) return;

        // 自动从 CircleCollider2D 获取半径写入 PlayerStats
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            _stats.ColliderRadius = circle.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        }
    }

    void Update()
    {
        if (_stats != null && _stats.IsDead) return;

        // 移动输入
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(horizontal, vertical);
        _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);

        // 攻击输入：通过委托转发
        if (Input.GetMouseButtonDown(0))
        {
            OnAttackInput?.Invoke();
        }
    }

    void FixedUpdate()
    {
        if (_rb == null || _stats == null) return;
        if (_stats.IsDead) return;

        float moveSpeed = _stats.MoveSpeed;
        float colliderRadius = _stats.ColliderRadius;

        Vector2 targetVelocity = _moveInput * moveSpeed;
        Vector3 currentPos = _rb.position;
        Vector3 nextPos = currentPos + (Vector3)targetVelocity * Time.fixedDeltaTime;

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

        if (canMove)
        {
            _rb.MovePosition(nextPos);
        }
        else
        {
            // 分轴滑动，防止卡墙
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
            _rb.MovePosition(finalPos);
        }
    }
}
