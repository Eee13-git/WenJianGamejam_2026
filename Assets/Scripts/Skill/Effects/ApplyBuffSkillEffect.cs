using UnityEngine;

/// <summary>
/// 泛用"施加 Buff"技能效果 — 执行时给施法者自身挂上一个 buff。
/// 适合自增益类技能（护盾、强化、免疫等）：只需配置 BuffData 即可复用。
/// 右键 -> Create -> Game -> Skill Effect -> Apply Buff
/// </summary>
[CreateAssetMenu(fileName = "ApplyBuffEffect", menuName = "Game/Skill Effect/Apply Buff")]
public class ApplyBuffSkillEffect : SkillEffectBase
{
    [Header("Buff 配置")]
    [Tooltip("要施加到施法者身上的 BuffData（Buff/Debuff 均可）")]
    [SerializeField] private BuffData _buffData;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        if (_buffData == null)
        {
            Debug.LogWarning("ApplyBuffSkillEffect: _buffData 为空！");
            return;
        }

        var target = caster.CasterTransform.gameObject;
        var buffManager = target.GetComponent<BuffManager>();
        if (buffManager == null)
        {
            Debug.LogWarning($"ApplyBuffSkillEffect: 目标 {target.name} 没有 BuffManager！");
            return;
        }

        buffManager.ApplyBuff(_buffData, target);
    }
}
