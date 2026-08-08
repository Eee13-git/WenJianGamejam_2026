using UnityEngine;

/// <summary>
/// 镇痛阻滞 buff 效果：
/// OnApply — 激活 AnalgesicBlockRuntime（伤害 50% 立即扣、50% 延迟），
///           角色免疫硬直（PlayerController.IgnoreStun = true）。
/// OnRemove — 把延迟伤害池转为"痛觉残留"debuff 缓慢扣除，恢复硬直免疫。
/// 泛用减益/增益效果：作为"伤害延迟"类 buff 的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "AnalgesicBlockBuff", menuName = "Game/Buff Effect/Analgesic Block")]
public class AnalgesicBlockBuff : BuffEffectBase
{
    [Header("镇痛配置")]
    [Tooltip("立即扣除比例（0.5 = 一半立即扣一半延迟）")]
    public float immediateFactor = 0.5f;

    [Tooltip("buff 结束后的痛觉残留 debuff（DamageOverTime 风格，动态总量）")]
    public BuffData afterDebuff;

    [Tooltip("镇痛光膜材质（null=无视觉）")]
    public Material fieldMaterial;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        // 激活/创建伤害延迟运行时组件
        var runtime = target.GetComponent<AnalgesicBlockRuntime>();
        if (runtime == null)
            runtime = target.AddComponent<AnalgesicBlockRuntime>();

        runtime.ImmediateFactor = immediateFactor;
        runtime.IsActive = true;
        runtime.Activate(fieldMaterial);

        // 硬直免疫（玩家）
        var pc = target.GetComponent<PlayerController>();
        if (pc != null)
            pc.IgnoreStun = true;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        var runtime = target.GetComponent<AnalgesicBlockRuntime>();

        // 恢复硬直免疫
        var pc = target.GetComponent<PlayerController>();
        if (pc != null)
            pc.IgnoreStun = false;

        // 有延迟伤害 → 转成"痛觉残留"debuff 缓慢扣除
        if (runtime != null && runtime.HasPending && afterDebuff != null)
        {
            var buffManager = target.GetComponent<BuffManager>();
            if (buffManager != null)
                buffManager.ApplyBuff(afterDebuff, buff.Caster);
        }

        // 清理运行时组件（延迟池已被 DOT 取走或清零）
        if (runtime != null)
            Object.Destroy(runtime);
    }
}
