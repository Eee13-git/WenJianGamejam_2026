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

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外 Buff 时长（秒，0=不成长）。作用于 BuffInstance 的时长覆盖，Refresh 叠加时保持升级后时长")]
    public float buffDurationPerLevel = 0f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
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

        // 先移除已有同 ID buff，强制重新触发 OnApply（一次性效果需要重触发，
        // 否则 ApplyBuff 同 ID 分支只刷新时长不调 OnApply，效果不会再次执行）
        if (buffManager.HasBuff(_buffData.buffId))
            buffManager.RemoveBuffById(_buffData.buffId);

        // 传入技能等级（Buff 效果可据此成长机制参数，如护盾量比例）
        var buff = buffManager.ApplyBuff(_buffData, target, level);

        // 机制成长：buff 时长随等级提升（覆盖资产时长；Refresh 叠加时保持升级后时长）
        if (buff != null && buffDurationPerLevel > 0f && level > 1)
            buff.SetDurationOverride(_buffData.duration + (level - 1) * buffDurationPerLevel);
    }
}
