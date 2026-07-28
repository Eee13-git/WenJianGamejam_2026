using UnityEngine;

/// <summary>
/// 技能效果抽象基类 — 策略模式的核心。
/// 每个具体效果都是一个独立的 ScriptableObject 资产。
/// </summary>
public abstract class SkillEffectBase : ScriptableObject
{
    /// <summary>
    /// 执行技能效果。
    /// </summary>
    /// <param name="caster">施法者接口（玩家或敌人）</param>
    /// <param name="direction">施法方向（已归一化）</param>
    /// <param name="damageMultiplier">经过等级加成后的最终伤害系数</param>
    /// <param name="ownerType">施法者类型，用于投射物区分敌我</param>
    public abstract void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType);
}
