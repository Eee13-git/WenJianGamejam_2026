using UnityEngine;

/// <summary>
/// 受伤触发 Buff — 挂载者受伤时触发效果。
/// 适用于 "反伤" "受伤无敌" "受伤加速" 等效果。
///
/// 示例:
///   "荆棘铠甲" — reflectPercent=0.3f（反弹 30% 伤害）
///   "紧急护盾" — invincibleDuration=1f（受伤后无敌 1 秒）
/// </summary>
[CreateAssetMenu(fileName = "OnDamagedBuff", menuName = "Game/Buff Effect/On Damaged")]
public class OnDamagedBuff : BuffEffectBase
{
    [Header("反伤")]
    [Tooltip("反伤百分比: 0.3 = 反弹 30% 伤害")]
    [Range(0f, 1f)] public float reflectPercent;

    [Header("无敌")]
    [Tooltip("受伤后无敌时间（秒），0 = 不触发无敌")]
    public float invincibleDuration;

    // 存储上次生命值，用于计算实际伤害量
    private float _lastHealth;
    private PlayerStats _stats;
    private GameObject _owner;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _owner = target;
        _stats = target.GetComponent<PlayerStats>();
        if (_stats != null)
        {
            _lastHealth = _stats.CurrentHealth;
            _stats.OnHealthChanged += HandleHealthChanged;
        }
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (_stats != null)
            _stats.OnHealthChanged -= HandleHealthChanged;
        _stats = null;
        _owner = null;
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (_stats == null) return;

        float damageTaken = _lastHealth - current;

        // 只处理受伤（生命减少）
        if (damageTaken <= 0f)
        {
            _lastHealth = current;
            return;
        }

        _lastHealth = current;

        // 反伤 — 查找最近的敌人并造成伤害
        if (reflectPercent > 0f && _owner != null)
        {
            float reflectDamage = damageTaken * reflectPercent;
            ApplyReflectDamage(reflectDamage);
        }

        // 无敌 — 通过 DamageImmunity 组件授予临时无敌
        if (invincibleDuration > 0f && _owner != null)
        {
            var immunity = _owner.GetComponent<DamageImmunity>();
            if (immunity == null)
                immunity = _owner.AddComponent<DamageImmunity>();
            immunity.GrantImmunity(invincibleDuration);
        }
    }

    private void ApplyReflectDamage(float damage)
    {
        if (_owner == null) return;

        // 查找附近的敌人
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject closest = null;
        float minDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            float dist = Vector2.Distance(_owner.transform.position, enemy.transform.position);
            if (dist < minDist && dist < 5f) // 5 单位搜索半径
            {
                minDist = dist;
                closest = enemy;
            }
        }

        if (closest != null)
        {
            var damageable = closest.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage);
        }
    }
}
