using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 痛觉残留 debuff 效果 — buff 生效期间把"镇痛阻滞"积累的延迟伤害缓慢扣除。
/// 总量在 OnApply 时从 AnalgesicBlockRuntime 读取（动态），按 tick 匀速扣完。
/// 作为"延迟伤害结算"类 debuff 的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "AnalgesicAfterBuff", menuName = "Game/Buff Effect/Analgesic Aftermath")]
public class AnalgesicAfterBuff : BuffEffectBase
{
    [Tooltip("结算间隔（秒）")]
    public float tickInterval = 0.5f;

    // buff → 剩余总伤害
    private static readonly Dictionary<BuffInstance, float> _remaining = new();
    // buff → tick 计时
    private static readonly Dictionary<BuffInstance, float> _timers = new();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        // 从镇痛运行时组件取走延迟伤害池作为总伤害
        float total = 0f;
        var runtime = target.GetComponent<AnalgesicBlockRuntime>();
        if (runtime != null)
            total = runtime.TakePendingDamage();

        _remaining[buff] = total;
        _timers[buff] = 0f;
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime)
    {
        if (!_remaining.TryGetValue(buff, out float total) || total <= 0f)
            return;

        _timers.TryGetValue(buff, out float timer);
        timer += deltaTime;
        if (timer < tickInterval)
        {
            _timers[buff] = timer;
            return;
        }

        // 每 tick 扣除量 = 总伤害 / 总时长 × tickInterval（匀速）
        float duration = Mathf.Max(buff.Data.duration, 0.1f);
        float perTick = total / duration * tickInterval;

        // 最后一 tick 不超总量
        perTick = Mathf.Min(perTick, total);

        // 扣血 + 同步池（保持可视化）
        var damageable = target.GetComponent<IDamageable>();
        damageable?.TakeDamage(perTick);

        var runtime = target.GetComponent<AnalgesicBlockRuntime>();
        if (runtime != null) runtime.ConsumePending(perTick);

        _remaining[buff] = total - perTick;
        _timers[buff] = timer - tickInterval;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        _remaining.Remove(buff);
        _timers.Remove(buff);
    }
}
