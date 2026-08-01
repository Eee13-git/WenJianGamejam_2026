using System;
using UnityEngine;

/// <summary>
/// 条件触发道具效果 — 监听指定事件，条件满足时执行子效果。
/// 支持叠加持有：首次获取时订阅事件，最后一次移除时取消订阅。
///
/// 示例:
///   "复仇之盾" — triggerEvent=OnDamaged, executeEffect=AoE伤害效果
///   "不死鸟之羽" — triggerEvent=OnHealthBelow(20%), executeEffect=回满血效果
/// </summary>
[CreateAssetMenu(fileName = "ConditionalEffect", menuName = "Game/Item Effect/Conditional")]
public class ConditionalEffect : ItemEffectBase
{
    [Header("触发条件")]
    [Tooltip("触发事件类型")]
    public ItemTriggerEvent triggerEvent;

    [Header("条件参数")]
    [Tooltip("OnHealthBelow 时的阈值（0-1，如 0.2 = 20%）")]
    [Range(0f, 1f)] public float healthThreshold = 0.2f;

    [Header("执行效果")]
    [Tooltip("条件满足时执行的效果（可嵌套其他 ItemEffectBase）")]
    public ItemEffectBase executeEffect;

    [Header("冷却与次数")]
    [Tooltip("触发冷却（秒），0 = 无冷却")]
    public float cooldown;

    [Tooltip("最大触发次数，0 = 无限")]
    public int maxTriggerCount;

    private float _lastTriggerTime = -999f;
    private int _triggerCount;
    private int _refCount; // 持有层数

    // 缓存的组件引用
    private PlayerStats _stats;
    private PlayerCombat _combat;
    private PlayerSkillManager _skillManager;

    public override void OnAcquire(GameObject owner)
    {
        if (_refCount == 0)
        {
            // 首次获取：缓存引用并订阅事件
            _stats = owner.GetComponent<PlayerStats>();
            _combat = owner.GetComponent<PlayerCombat>();
            _skillManager = owner.GetComponent<PlayerSkillManager>();
            SubscribeEvents();
        }
        _refCount++;
    }

    public override void OnRemove(GameObject owner)
    {
        _refCount--;
        if (_refCount <= 0)
        {
            _refCount = 0;
            UnsubscribeEvents();
            _stats = null;
            _combat = null;
            _skillManager = null;
        }
    }

    private void SubscribeEvents()
    {
        switch (triggerEvent)
        {
            case ItemTriggerEvent.OnDamaged:
                if (_stats != null) _stats.OnHealthChanged += HandleHealthChanged;
                break;
            case ItemTriggerEvent.OnAttack:
                if (_combat != null) _combat.OnShoot += HandleAttack;
                break;
            case ItemTriggerEvent.OnSkillCast:
                if (_skillManager != null) _skillManager.OnSkillCast += HandleSkillCast;
                break;
            case ItemTriggerEvent.OnHealthBelow:
                if (_stats != null) _stats.OnHealthChanged += HandleHealthBelow;
                break;
        }
    }

    private void UnsubscribeEvents()
    {
        if (_stats != null)
        {
            _stats.OnHealthChanged -= HandleHealthChanged;
            _stats.OnHealthChanged -= HandleHealthBelow;
        }
        if (_combat != null)
            _combat.OnShoot -= HandleAttack;
        if (_skillManager != null)
            _skillManager.OnSkillCast -= HandleSkillCast;
    }

    private void TryTrigger(GameObject owner)
    {
        if (cooldown > 0f && Time.time - _lastTriggerTime < cooldown)
            return;
        if (maxTriggerCount > 0 && _triggerCount >= maxTriggerCount)
            return;

        _lastTriggerTime = Time.time;
        _triggerCount++;
        executeEffect?.OnAcquire(owner);
    }

    private void HandleHealthChanged(float current, float max) { }
    private void HandleAttack(Vector2 direction, float bulletSpeed, float attackStrength)
    {
        TryTrigger(_stats?.gameObject);
    }
    private void HandleSkillCast(int slotIndex, SkillData data)
    {
        TryTrigger(_stats?.gameObject);
    }
    private void HandleHealthBelow(float current, float max)
    {
        if (current / max <= healthThreshold)
            TryTrigger(_stats?.gameObject);
    }
}

/// <summary>
/// 道具触发事件类型
/// </summary>
public enum ItemTriggerEvent
{
    OnAttack,
    OnDamaged,
    OnKill,
    OnRoomEnter,
    OnRoomClear,
    OnHealthBelow,
    OnSkillCast,
    OnDodge
}
