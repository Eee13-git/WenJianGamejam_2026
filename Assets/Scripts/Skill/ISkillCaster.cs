using UnityEngine;

/// <summary>
/// 技能施法者接口，统一玩家与敌人的技能释放逻辑。
/// </summary>
public interface ISkillCaster
{
    /// <summary>施法者的 Transform</summary>
    Transform CasterTransform { get; }

    /// <summary>获取目标方向（玩家=鼠标方向，敌人=AI逻辑）</summary>
    Vector2 GetTargetDirection();

    /// <summary>获取攻击力，用于技能伤害计算</summary>
    float GetAttackStrength();

    /// <summary>施法者阵营：Player=玩家方（打Enemy） / Enemy=敌方（打Player）</summary>
    Projectile.OwnerType GetOwnerType();

    /// <summary>技能伤害修正乘区（道具/buff 可修改），默认 1.0</summary>
    float GetSkillDamageModifier();
}
