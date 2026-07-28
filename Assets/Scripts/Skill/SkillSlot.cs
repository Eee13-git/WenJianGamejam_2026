using System;
using UnityEngine;

/// <summary>
/// 技能槽位：绑定键位 + 技能实例 + 解锁状态。
/// 在 Inspector 中可配置初始键位。
/// </summary>
[Serializable]
public class SkillSlot
{
    public KeyCode keyBinding = KeyCode.None;
    public bool isUnlocked = true;

    public SkillInstance Skill { get; private set; }
    public bool IsEmpty => Skill == null;

    public void Equip(SkillInstance skill) => Skill = skill;
    public void Unequip() => Skill = null;

    /// <summary>尝试释放槽位中的技能</summary>
    public bool TryCast(ISkillCaster caster, Vector2 direction)
    {
        return Skill != null && Skill.TryCast(caster, direction);
    }
}
