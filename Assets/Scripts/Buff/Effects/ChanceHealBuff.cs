using UnityEngine;

/// <summary>
/// 概率回血 Buff — 每 tickInterval 秒以 chance 概率回 healAmount 点血。
/// 注意：需用 isPermanent=false + 超长 duration（永久 buff 不执行 OnTick）。
/// </summary>
[CreateAssetMenu(fileName = "ChanceHealBuff", menuName = "Game/Buff Effect/Chance Heal")]
public class ChanceHealBuff : BuffEffectBase
{
    [Header("治疗配置")]
    [Tooltip("每次触发回复的血量")]
    [SerializeField] private float _healAmount = 10f;
    [Tooltip("触发间隔（秒）")]
    [SerializeField] private float _tickInterval = 60f;
    [Tooltip("触发概率 (0~1)")]
    [SerializeField] private float _chance = 0.5f;

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
        if (timer < _tickInterval)
        {
            _tickTimers[buff] = timer;
            return;
        }

        int tickCount = Mathf.FloorToInt(timer / _tickInterval);
        _tickTimers[buff] = timer - _tickInterval * tickCount;

        for (int i = 0; i < tickCount; i++)
        {
            if (Random.value < _chance)
            {
                var healable = target.GetComponent<IHealable>();
                healable?.Heal(_healAmount);
            }
        }
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        _tickTimers.Remove(buff);
    }
}
