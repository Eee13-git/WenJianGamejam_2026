using UnityEngine;

/// <summary>
/// 持续伤害 Buff (Damage Over Time) — 每 tickInterval 秒对目标造成 damagePerTick * stacks 点伤害。
/// 示例: 中毒 — 每秒 5 伤害，持续 6 秒
/// </summary>
[CreateAssetMenu(fileName = "DOTBuff", menuName = "Game/Buff Effect/Damage Over Time")]
public class DamageOverTimeBuff : BuffEffectBase
{
    [Header("伤害配置")]
    [Tooltip("每次 Tick 造成的伤害（乘以堆叠数）")]
    public float damagePerTick = 5f;

    [Tooltip("Tick 间隔（秒）")]
    public float tickInterval = 1f;

    // 存储在 BuffInstance 上的数据 — 使用 BuffInstance 的 ElapsedTime 派生 tickTimer
    // 更简单的方案: 直接用字典映射 buff → tickTimer
    private static readonly System.Collections.Generic.Dictionary<BuffInstance, float> _tickTimers
        = new System.Collections.Generic.Dictionary<BuffInstance, float>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _tickTimers[buff] = 0f;
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime)
    {
        if (!_tickTimers.TryGetValue(buff, out float timer))
        {
            timer = 0f;
            _tickTimers[buff] = timer;
        }

        timer += deltaTime;
        if (timer < tickInterval)
        {
            _tickTimers[buff] = timer;
            return;
        }

        // 累积多次 tick（如果帧间隔大于 tickInterval）
        int tickCount = Mathf.FloorToInt(timer / tickInterval);
        // 每 tick 伤害：优先使用技能区域设置的覆盖值（按施法者攻击力缩放），否则用资产配置值
        float perTick = buff.TickDamageOverride >= 0f ? buff.TickDamageOverride : damagePerTick;
        float totalDmg = perTick * buff.CurrentStacks * tickCount;

        _tickTimers[buff] = timer - tickInterval * tickCount;

        var damageable = target.GetComponent<IDamageable>();
        damageable?.TakeDamage(totalDmg);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        _tickTimers.Remove(buff);
    }
}
