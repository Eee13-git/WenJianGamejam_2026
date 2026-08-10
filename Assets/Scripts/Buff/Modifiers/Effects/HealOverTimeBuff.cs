using UnityEngine;

/// <summary>
/// 持续治疗 Buff (Heal Over Time) — 每 tickInterval 秒对目标治疗 healPerTick * stacks 点生命。
/// 示例: 回血光环 — 每秒 3 治疗，持续 8 秒
/// </summary>
[CreateAssetMenu(fileName = "HOTBuff", menuName = "Game/Buff Effect/Heal Over Time")]
public class HealOverTimeBuff : BuffEffectBase
{
    [Header("治疗配置")]
    [Tooltip("每次 Tick 的治疗量（乘以堆叠数）")]
    public float healPerTick = 3f;

    [Tooltip("Tick 间隔（秒）")]
    public float tickInterval = 1f;

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

        int tickCount = Mathf.FloorToInt(timer / tickInterval);
        float totalHeal = healPerTick * buff.CurrentStacks * tickCount;

        _tickTimers[buff] = timer - tickInterval * tickCount;

        var healable = target.GetComponent<IHealable>();
        healable?.Heal(totalHeal);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        _tickTimers.Remove(buff);
    }
}
