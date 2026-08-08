using UnityEngine;

/// <summary>
/// 角质增生覆膜技能效果 — 获得护盾，可抵挡弹幕伤害和冲撞伤害。
/// 可长按持续架盾（玩家按住技能键；松开提前结束），架盾期间不可攻击且降低移速，最长 duration 秒。
/// 释放后 perfectWindow 秒内被击中 → 完美格挡 → 立即刷新该技能冷却。
/// 泛用护盾效果：时长/减速/完美窗口/护盾材质均可配置。
/// 右键 -> Create -> Game -> Skill Effect -> Keratin Shield
/// </summary>
[CreateAssetMenu(fileName = "KeratinShieldEffect", menuName = "Game/Skill Effect/Keratin Shield")]
public class KeratinShieldSkillEffect : SkillEffectBase
{
    [Header("护盾配置")]
    [Tooltip("架盾最长持续时间（秒）")]
    [SerializeField] private float _duration = 3f;
    [Tooltip("架盾期间移速倍率（0.1~1，越小越慢）")]
    [SerializeField] private float _slowFactor = 0.5f;
    [Tooltip("完美格挡窗口（秒）：释放后此时间内被击中 → 刷新冷却")]
    [SerializeField] private float _perfectWindow = 0.3f;
    [Tooltip("格挡后无敌时长（秒）：成功格挡后这段时间内免疫所有伤害，0=无")]
    [SerializeField] private float _immuneDuration = 1.5f;
    [Tooltip("本技能 skillId（完美格挡时刷新冷却用）")]
    [SerializeField] private string _skillId = "keratin_shield";
    [Tooltip("护盾视觉材质（null=无视觉）")]
    [SerializeField] private Material _shieldMaterial;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        // 已有护盾架设中则不重复触发
        var existing = caster.CasterTransform.GetComponent<KeratinShieldRuntime>();
        if (existing != null && existing.IsActive) return;

        var shield = caster.CasterTransform.gameObject.AddComponent<KeratinShieldRuntime>();
        shield.Activate(caster, _duration, _slowFactor, _perfectWindow, _immuneDuration, _skillId, _shieldMaterial);
    }
}
