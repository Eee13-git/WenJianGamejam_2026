using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 周期性护盾 buff 效果 — 定期给目标生成一个伤害吸收护盾。
/// 护盾 HP = 目标最大生命值 × shieldRatio；
/// 护盾持续 shieldDuration 秒，期间获得 damageReduction 比例的免伤；
/// 护盾被击碎时目标僵直 stunDuration 秒（禁用移动+技能）；
/// 僵直结束后重新开始计时，周期性重新生成护盾。
/// 泛用类：护盾比例、免伤比例、持续时间、僵直时长均可配置。
/// 右键 -> Create -> Game -> Buff Effect -> Periodic Shield
/// </summary>
[CreateAssetMenu(fileName = "PeriodicShieldBuff", menuName = "Game/Buff Effect/Periodic Shield")]
public class PeriodicShieldBuff : BuffEffectBase
{
    [Header("护盾配置")]
    [Tooltip("护盾 HP = 最大生命值 × 此比例")]
    public float shieldRatio = 0.3f;
    [Tooltip("护盾期间的免伤比例（0=无减免，0.5=减半）")]
    public float damageReduction = 0.5f;
    [Tooltip("护盾持续时间（秒）")]
    public float shieldDuration = 5f;
    [Tooltip("护盾生成间隔（秒，护盾消失/破碎后多久重新生成）")]
    public float regenInterval = 3f;

    [Header("破碎僵直")]
    [Tooltip("护盾被击碎时的僵直时间（秒）")]
    public float stunDuration = 3f;

    [Header("视觉")]
    [Tooltip("护盾视觉材质（null=无视觉）")]
    public Material shieldMaterial;

    private static readonly Dictionary<BuffInstance, float> _regenTimers = new Dictionary<BuffInstance, float>();
    private static readonly Dictionary<BuffInstance, bool> _shieldActive = new Dictionary<BuffInstance, bool>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _regenTimers[buff] = 0f;
        _shieldActive[buff] = false;

        // 立即生成第一个护盾
        CreateShield(target, buff);
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime)
    {
        // 检测护盾是否已消失（破碎或过期）
        if (_shieldActive.GetValueOrDefault(buff))
        {
            var shield = target.GetComponent<AbsorbShield>();
            if (shield == null || !shield.IsActive)
            {
                _shieldActive[buff] = false;
                _regenTimers[buff] = 0f;
            }
            return;
        }

        // 僵直期间不计时
        if (IsStunned(target)) return;

        // 计时 → 重新生成护盾
        float timer = _regenTimers.GetValueOrDefault(buff) + deltaTime;
        _regenTimers[buff] = timer;

        if (timer >= regenInterval)
        {
            _regenTimers[buff] = 0f;
            CreateShield(target, buff);
        }
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        _regenTimers.Remove(buff);
        _shieldActive.Remove(buff);

        var shield = target.GetComponent<AbsorbShield>();
        if (shield != null) Destroy(shield);
    }

    private void CreateShield(GameObject target, BuffInstance buff)
    {
        float maxHP = 1f;
        var enemyStats = target.GetComponent<EnemyStats>();
        if (enemyStats != null) maxHP = enemyStats.MaxHealth;
        else
        {
            var playerStats = target.GetComponent<PlayerStats>();
            if (playerStats != null) maxHP = playerStats.MaxHealth;
        }

        float shieldHP = maxHP * shieldRatio;

        Debug.Log($"[PeriodicShield] CreateShield: target={target.name}, maxHP={maxHP}, shieldHP={shieldHP}, shieldMat={shieldMaterial?.name}");

        var shield = target.GetComponent<AbsorbShield>();
        if (shield != null) Destroy(shield);

        shield = target.AddComponent<AbsorbShield>();
        shield.Initialize(shieldHP, damageReduction, shieldDuration, shieldMaterial);

        _shieldActive[buff] = true;

        shield.OnShieldBroken += () =>
        {
            _shieldActive[buff] = false;
            _regenTimers[buff] = 0f;
            ApplyStun(target);
        };
    }

    /// <summary>检查目标是否正在僵直中</summary>
    private bool IsStunned(GameObject target)
    {
        var movement = target.GetComponent<EnemyMovement>();
        if (movement != null && !movement.enabled) return true;

        var playerController = target.GetComponent<PlayerController>();
        if (playerController != null && playerController.InputLocked) return true;

        return false;
    }

    /// <summary>施加僵直（禁用移动+技能若干秒）</summary>
    private void ApplyStun(GameObject target)
    {
        var movement = target.GetComponent<EnemyMovement>();
        var skillManager = target.GetComponent<EnemySkillManager>();
        var stateMachine = target.GetComponent<EnemyStateMachine>();

        if (movement != null) movement.Stop();

        // 使用一个临时组件管理僵直恢复
        var stun = target.AddComponent<StunController>();
        stun.Run(stunDuration, movement, skillManager, stateMachine);

        Debug.Log($"[PeriodicShield] 护盾破碎 → {target.name} 僵直 {stunDuration}s");
    }
}

/// <summary>临时僵直控制器 — 僵直期间禁用移动+技能，到期恢复</summary>
public class StunController : MonoBehaviour
{
    private float _timer;
    private float _duration;
    private EnemyMovement _movement;
    private EnemySkillManager _skillManager;
    private EnemyStateMachine _stateMachine;
    private bool _restored;

    public void Run(float duration, EnemyMovement movement, EnemySkillManager skillManager, EnemyStateMachine stateMachine)
    {
        _duration = duration;
        _timer = 0f;
        _movement = movement;
        _skillManager = skillManager;
        _stateMachine = stateMachine;
        _restored = false;

        // 禁用
        if (movement != null) movement.enabled = false;
        if (skillManager != null) skillManager.enabled = false;
    }

    private void Update()
    {
        if (_restored) return;

        _timer += Time.deltaTime;
        if (_timer >= _duration)
        {
            // 恢复
            if (_movement != null) _movement.enabled = true;
            if (_skillManager != null) _skillManager.enabled = true;
            _restored = true;
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (!_restored)
        {
            if (_movement != null) _movement.enabled = true;
            if (_skillManager != null) _skillManager.enabled = true;
        }
    }
}
