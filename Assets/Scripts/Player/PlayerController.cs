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

    /// <summary>输入是否被锁定 (房间切换期间)</summary>
    public bool InputLocked { get; set; }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _combat = GetComponent<PlayerCombat>();
        _stats = GetComponent<PlayerStats>();

        if (_combat != null)
            OnAttackInput += _combat.TryShoot;

#if UNITY_EDITOR
        if (_rb == null)
            Debug.LogWarning("PlayerController: 未找到 Rigidbody2D 组件！");
        if (_combat == null)
            Debug.LogWarning("PlayerController: 未找到 PlayerCombat 组件！");
        if (_stats == null)
            Debug.LogWarning("PlayerController: 未找到 PlayerStats 组件！");
#endif
    }

    void Start()
    {
        if (_stats == null) return;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            _stats.ColliderRadius = circle.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        }

        // Start 时 MapManager.Instance 已经初始化 (Awake 时序保证)
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnRoomSwitchStarted += (f, t) => InputLocked = true;
            MapManager.Instance.OnRoomSwitchCompleted += (t) => InputLocked = false;
        }
    }

    void Update()
    {
        if (_stats != null && _stats.IsDead) return;
        if (InputLocked)
        {
            _moveInput = Vector2.zero;
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(horizontal, vertical);
        _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);

        if (Input.GetMouseButtonDown(0))
        {
            OnAttackInput?.Invoke();
        }
    }

    void FixedUpdate()
    {
        if (_rb == null || _stats == null) return;
        if (_stats.IsDead) return;
        if (InputLocked) return;

        // 使用 Rigidbody2D 物理驱动，墙壁碰撞由 Collider2D 处理
        _rb.velocity = _moveInput * _stats.MoveSpeed;
    }
}
