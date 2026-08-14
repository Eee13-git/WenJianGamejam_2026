using UnityEngine;

/// <summary>
/// ICU 锁定 Buff — 攻击力、弹速、攻速、移速只会往上升不会往下降。
/// 锁定角色拥有过的最高数值，读取时取 Max(历史最高, 实际值)。
/// 不修改实际数值。
///
/// 攻击力锁定的是 base×multiplier 复合值（实时记录最高攻击），
/// 任意一项降低时，只要复合值低于历史最高，读取仍返回历史最高。
/// </summary>
[CreateAssetMenu(fileName = "IcuLockBuff", menuName = "Game/Buff Effect/ICU Lock")]
public class IcuLockBuff : BuffEffectBase
{
    // 单值锁定属性（直接锁定原始值）
    private static readonly string[] TrackedStats =
    {
        "BulletSpeed",
        "ShotsPerMinute",
        "MoveSpeed"
    };

    // 触发攻击力复合值重算的属性名
    private static readonly string[] AttackTriggerStats =
    {
        "AttackStrength",
        "AttackStrengthMultiplier"
    };

    private PlayerStats _stats;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _stats = target.GetComponent<PlayerStats>();
        if (_stats == null)
        {
            Debug.LogWarning($"IcuLockBuff: target '{target.name}' 没有 PlayerStats 组件");
            return;
        }

        // 记录当前值作为锁定最小值
        foreach (var statName in TrackedStats)
        {
            _stats.SetLockedMinimum(statName, _stats.GetRawStatValue(statName));
        }

        // 记录当前复合攻击力作为锁定最高值
        UpdateLockedMaxAttack();

        // 订阅属性变化，追踪上升
        _stats.OnStatChanged += OnStatChanged;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (_stats == null) return;

        _stats.OnStatChanged -= OnStatChanged;

        foreach (var statName in TrackedStats)
        {
            _stats.ClearLockedMinimum(statName);
        }

        _stats.ClearLockedMaxAttack();
    }

    private void OnStatChanged(string statName)
    {
        if (_stats == null) return;

        // 单值锁定属性
        foreach (var s in TrackedStats)
        {
            if (s == statName)
            {
                float actualValue = _stats.GetRawStatValue(statName);
                _stats.SetLockedMinimum(statName, actualValue);
                return;
            }
        }

        // 攻击力复合值重算
        foreach (var s in AttackTriggerStats)
        {
            if (s == statName)
            {
                UpdateLockedMaxAttack();
                return;
            }
        }
    }

    /// <summary>读取当前复合攻击力（base×multiplier），更新锁定最高值</summary>
    private void UpdateLockedMaxAttack()
    {
        if (_stats == null) return;
        float baseVal = _stats.GetRawStatValue("AttackStrength");
        float multiplier = _stats.GetRawStatValue("AttackStrengthMultiplier");
        _stats.SetLockedMaxAttack(baseVal * multiplier);
    }
}
