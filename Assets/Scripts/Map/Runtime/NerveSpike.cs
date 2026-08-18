using UnityEngine;

/// <summary>
/// 神经刺陷阱：周期性切换三阶段 sprite，仅状态3（完全探出）时造成伤害。
/// 命中后切换到状态2第二阶段，计时跳到 3s 位置。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class NerveSpike : MonoBehaviour
{
    [Header("Sprite")]
    [SerializeField] private Sprite _state1Sprite;  // 未探出
    [SerializeField] private Sprite _state2Sprite;  // 将探出
    [SerializeField] private Sprite _state3Sprite;  // 完全探出

    [Header("时序 (秒)")]
    [SerializeField] private float _state1End = 1f;
    [SerializeField] private float _state2FirstEnd = 0.5f;
    [SerializeField] private float _state3End = 1.5f;
    [SerializeField] private float _state2SecondEnd = 0.5f;

    [Header("伤害")]
    [Tooltip("伤害比例（占最大生命值）")]
    [SerializeField] private float _damageRatio = 0.1f;
    [SerializeField] private float _damageCooldown = 0.5f;

    private enum SpikeState { State1, State2First, State3, State2Second }

    private SpikeState _state;
    private float _timer;
    private SpriteRenderer _sr;
    private float _lastDamageTime;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        SetState(SpikeState.State1);
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        switch (_state)
        {
            case SpikeState.State1:
                if (_timer >= _state1End) SetState(SpikeState.State2First);
                break;
            case SpikeState.State2First:
                if (_timer >= _state2FirstEnd) SetState(SpikeState.State3);
                break;
            case SpikeState.State3:
                if (_timer >= _state3End) SetState(SpikeState.State2Second);
                break;
            case SpikeState.State2Second:
                if (_timer >= _state2SecondEnd) SetState(SpikeState.State1);
                break;
        }
    }

    private void SetState(SpikeState newState)
    {
        _state = newState;
        _timer = 0f;

        Sprite target = newState switch
        {
            SpikeState.State1 => _state1Sprite,
            SpikeState.State2First => _state2Sprite,
            SpikeState.State3 => _state3Sprite,
            SpikeState.State2Second => _state2Sprite,
            _ => null
        };

        if (_sr != null && target != null)
            _sr.sprite = target;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (_state != SpikeState.State3) return;
        if (Time.time < _lastDamageTime + _damageCooldown) return;

        var damageable = other.GetComponent<IDamageable>();
        if (damageable == null) return;

        var enemyCore = other.GetComponent<EnemyCore>();
        if (enemyCore != null && enemyCore.IsDead) return;

        var playerStats = other.GetComponent<PlayerStats>();
        if (playerStats != null && playerStats.IsDead) return;

        // 百分比伤害：按目标最大生命值计算
        float maxHP = 0f;
        if (playerStats != null)
            maxHP = playerStats.MaxHealth;
        else if (enemyCore != null && enemyCore.Health != null)
            maxHP = enemyCore.Health.MaxHealth;

        damageable.TakeDamage(maxHP * _damageRatio);
        _lastDamageTime = Time.time;

        // 命中后切换到状态2第二阶段，计时跳到 3s 位置
        SetState(SpikeState.State2Second);
    }

    /// <summary>运行时配置（供障碍物制造器调用）</summary>
    public void Configure(float damageRatio, float damageCooldown)
    {
        _damageRatio = damageRatio;
        _damageCooldown = damageCooldown;
    }
}
