using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

    /// <summary>外部注入速度（击退/气浪等）。FixedUpdate 优先使用并按 ExternalVelocityDecay 衰减至零，期间玩家输入被压制</summary>
    public Vector2 ExternalVelocity { get; set; }

    /// <summary>外部速度每秒衰减量（单位/秒²），击退后逐渐恢复玩家控制</summary>
    private const float ExternalVelocityDecay = 10f;

    /// <summary>当前是否有外部击退速度（供外部查询/调试）</summary>
    public bool HasExternalVelocity => ExternalVelocity.sqrMagnitude > 0.01f;

    /// <summary>清除外部速度（击退提前结束）</summary>
    public void ClearExternalVelocity() => ExternalVelocity = Vector2.zero;

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

        // 手柄摇杆漂移过滤：微小输入归零（阈值 0.1）
        if (Mathf.Abs(horizontal) < 0.1f) horizontal = 0f;
        if (Mathf.Abs(vertical) < 0.1f) vertical = 0f;

        Vector2 rawInput = new Vector2(horizontal, vertical);
        rawInput = Vector2.ClampMagnitude(rawInput, 1f);
        bool rawAttack = Input.GetMouseButton(0) && !AttackLocked && !IsPointerOverUI();

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

        // 输入锁定（房间切换/眩晕/喷射期间）：强制清零速度，防止残留 velocity 导致无限滑动
        // （Rigidbody2D.linearDrag=0，速度不会自然衰减，必须手动清零）
        if (InputLocked)
        {
            _rb.velocity = Vector2.zero;
            return;
        }

        // 外部击退/气浪速度优先：持续注入并衰减，期间玩家输入被压制（被推开）
        if (ExternalVelocity.sqrMagnitude > 0.01f)
        {
            _rb.velocity = ExternalVelocity;
            ExternalVelocity = Vector2.MoveTowards(ExternalVelocity, Vector2.zero, ExternalVelocityDecay * Time.fixedDeltaTime);
            return;
        }

        // 使用 Rigidbody2D 物理驱动，墙壁碰撞由 Collider2D 处理
        _rb.velocity = _moveInput * _stats.MoveSpeed * SpeedMultiplier;
    }

    /// <summary>鼠标是否悬停在 UI 上（用于阻止穿透 UI 的普攻）</summary>
    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }
}
