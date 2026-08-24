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
    /// <param name="level">技能当前等级（1=基础）。效果类据此应用"机制成长"（数量/半径/时长等）</param>
    public abstract void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level);

    /// <summary>
    /// 兼容旧调用（未指定等级时按 1 级执行）。亡语等无等级上下文的效果走此入口。
    /// </summary>
    public void Execute(ISkillCaster caster, Vector2 direction,
                        float damageMultiplier, Projectile.OwnerType ownerType)
    {
        Execute(caster, direction, damageMultiplier, ownerType, 1);
    }
}
