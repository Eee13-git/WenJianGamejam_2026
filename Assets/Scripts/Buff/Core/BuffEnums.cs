/// <summary>
/// Buff 类型: Buff(增益) / Debuff(减益) / Neutral(中性)
/// </summary>
public enum BuffType
{
    Buff,
    Debuff,
    Neutral
}

/// <summary>
/// Buff 叠加行为:
///   Refresh       — 刷新持续时间，堆叠不独立计时
///   Independent   — 每个堆叠独立计时（逐个过期）
///   ExtendDuration — 每次叠加延长持续时间
/// </summary>
public enum StackBehavior
{
    Refresh,
    Independent,
    ExtendDuration
}
