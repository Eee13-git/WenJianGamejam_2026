/// <summary>
/// 属性修改器 — 可序列化的值类型。
/// 支持加算 (Additive)、乘算 (Multiplicative)、覆盖 (Override) 三种模式。
/// Buff 系统通过 StatModifier 修改目标属性值。
/// </summary>
[System.Serializable]
public struct StatModifier
{
    public enum Mode
    {
        /// <summary>加算: result = baseValue + value</summary>
        Additive,

        /// <summary>乘算: result = baseValue * value</summary>
        Multiplicative,

        /// <summary>覆盖: result = value</summary>
        Override
    }

    public Mode mode;
    public float value;

    /// <summary>应用修改器到基础值</summary>
    public static float Apply(float baseValue, StatModifier modifier)
    {
        return modifier.mode switch
        {
            Mode.Additive       => baseValue + modifier.value,
            Mode.Multiplicative => baseValue * modifier.value,
            Mode.Override       => modifier.value,
            _                   => baseValue
        };
    }
}
