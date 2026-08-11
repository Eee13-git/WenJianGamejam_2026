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

    /// <summary>攻击锁定（架盾/引导技能期间不可普攻）</summary>
    public bool AttackLocked { get; set; }

    /// <summary>移动速度倍率（架盾减速等），默认 1</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>硬直免疫（镇痛阻滞等 buff 期间不受眩晕/硬直）</summary>
    public bool IgnoreStun { get; set; }

    /// <summary>输入延迟（秒），道具效果</summary>
    public static float InputDelay = 0f;

    private struct DelayedInput
    {
        public float timestamp;
        public Vector2 input;
        public bool attack;
    }
    private readonly System.Collections.Generic.List<DelayedInput> _inputBuffer = new();

    /// <summary>当前移动方向（归一化，供冲刺/位移技能读取）</summary>
    public Vector2 MoveDirection => _moveInput.sqrMagnitude > 0.01f ? _moveInput.normalized : Vector2.zero;

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
        Vector2 rawInput = new Vector2(horizontal, vertical);
        rawInput = Vector2.ClampMagnitude(rawInput, 1f);
        bool rawAttack = Input.GetMouseButton(0) && !AttackLocked;

        if (InputDelay <= 0f)
        {
            _moveInput = rawInput;
            if (rawAttack)
                OnAttackInput?.Invoke();
        }
        else
        {
            // 缓存输入，延迟应用
            _inputBuffer.Add(new DelayedInput { timestamp = Time.time, input = rawInput, attack = rawAttack });

            // 取出过期的输入
            float cutoff = Time.time - InputDelay;
            Vector2 delayedMove = Vector2.zero;
            bool delayedAttack = false;
            for (int i = _inputBuffer.Count - 1; i >= 0; i--)
            {
                if (_inputBuffer[i].timestamp <= cutoff)
                {
                    delayedMove = _inputBuffer[i].input;
                    delayedAttack = _inputBuffer[i].attack;
                    _inputBuffer.RemoveRange(0, i + 1);
                    break;
                }
            }

            _moveInput = delayedMove;
            if (delayedAttack)
                OnAttackInput?.Invoke();
        }
    }

    void FixedUpdate()
    {
        if (_rb == null || _stats == null) return;
        if (_stats.IsDead) return;
        if (InputLocked) return;

        // 使用 Rigidbody2D 物理驱动，墙壁碰撞由 Collider2D 处理
        _rb.velocity = _moveInput * _stats.MoveSpeed * SpeedMultiplier;
    }
}
