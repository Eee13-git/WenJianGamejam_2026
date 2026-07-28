using UnityEngine;

/// <summary>
/// 治疗技能效果：恢复施法者自身生命值。
/// 右键 -> Create -> Game -> Skill Effect -> Heal
/// </summary>
[CreateAssetMenu(fileName = "HealEffect", menuName = "Game/Skill Effect/Heal")]
public class HealSkillEffect : SkillEffectBase
{
    [Header("治疗配置")]
    [Tooltip("基础治疗量")]
    public float baseHealAmount = 20f;

    [Tooltip("是否受攻击力加成")]
    public bool scaleWithAttack = true;

    [Tooltip("治疗特效预制体（可选）")]
    public GameObject healVfx;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        float healAmount = scaleWithAttack
            ? baseHealAmount + caster.GetAttackStrength() * damageMultiplier * 0.5f
            : baseHealAmount;

        if (caster.CasterTransform.TryGetComponent<IHealable>(out var healable))
        {
            healable.Heal(healAmount);
        }

        if (healVfx != null)
            Instantiate(healVfx, caster.CasterTransform.position, Quaternion.identity);
    }
}
