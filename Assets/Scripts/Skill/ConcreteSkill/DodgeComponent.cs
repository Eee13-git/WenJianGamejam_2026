using UnityEngine;

/// <summary>
/// 受击闪避组件 — 挂在角色身上，即将受击时触发滑移位移，成功闪避则免疫本次伤害。
/// 由 DodgeOnDamagedBuff 在 OnApply 时添加、OnRemove 时移除。
/// EnemyStats.TakeDamage 入口检查此组件并调用 TryDodge()。
/// 泛用组件：闪避概率、滑移距离/速度/冷却均可配置。
/// </summary>
public class DodgeComponent : MonoBehaviour
{
    private float _dodgeChance = 1f;
    private float _dodgeCooldown = 2f;
    private float _slideDistance = 3f;
    private float _slideSpeed = 18f;
    private float _immuneDuration = 0.5f;

    private float _cooldownTimer;
    private bool _initialized;

    /// <summary>初始化闪避参数</summary>
    public void Initialize(float dodgeChance, float dodgeCooldown,
        float slideDistance, float slideSpeed, float immuneDuration)
    {
        _dodgeChance = dodgeChance;
        _dodgeCooldown = dodgeCooldown;
        _slideDistance = slideDistance;
        _slideSpeed = slideSpeed;
        _immuneDuration = immuneDuration;
        _cooldownTimer = 0f;
        _initialized = true;
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - Time.deltaTime);
    }

    /// <summary>尝试闪避。由 EnemyStats.TakeDamage 入口调用：成功则授予无敌免疫本次伤害并滑移。</summary>
    public bool TryDodge(Vector2 awayDirection)
    {
        if (!_initialized) return false;
        if (_cooldownTimer > 0f) return false;
        if (Random.value > _dodgeChance) return false;

        // 触发滑移（方向：远离攻击者）
        Vector2 slideDir = awayDirection;
        if (slideDir.sqrMagnitude < 0.01f)
            slideDir = Vector2.right;

        var caster = GetComponent<EnemyCore>();
        if (caster == null) return false;

        var slide = GetComponent<SlideRuntime>();
        if (slide == null)
            slide = gameObject.AddComponent<SlideRuntime>();
        slide.Activate(caster, _slideDistance, _slideSpeed, _immuneDuration, null);

        // 授予无敌（免疫本次伤害）
        var immunity = GetComponent<DamageImmunity>();
        if (immunity == null)
            immunity = gameObject.AddComponent<DamageImmunity>();
        immunity.GrantImmunity(_immuneDuration);

        // 开始冷却
        _cooldownTimer = _dodgeCooldown;

        Debug.Log($"[DodgeComponent] {gameObject.name} 受击闪避！");
        return true;
    }
}
