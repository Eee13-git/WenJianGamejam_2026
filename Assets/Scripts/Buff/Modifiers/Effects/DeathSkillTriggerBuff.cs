using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 泛用亡语技能 buff 效果 — 死亡触发：死亡瞬间执行指定的 SkillEffectBase。
/// 用于"死亡释放气浪""死亡范围击退"等机制。
/// 通过被动技能（ApplyBuffSkillEffect + 永久 buff）挂到敌人身上；
/// OnApply 订阅 EnemyCore.OnDied，死亡瞬间触发，OnRemove 取消订阅。
/// 右键 -> Create -> Game -> Buff Effect -> Death Skill Trigger
/// </summary>
[CreateAssetMenu(fileName = "DeathSkillTriggerBuff", menuName = "Game/Buff Effect/Death Skill Trigger")]
public class DeathSkillTriggerBuff : BuffEffectBase
{
    [Tooltip("死亡时执行的技能效果（fallback：未配置 deathSkillId 或角色身上无该技能时使用）")]
    public SkillEffectBase deathSkillEffect;

    [Tooltip("死亡时调用的角色自身技能 ID（从 EnemySkillManager 查找并执行，如 \"axon_block\"）。空=用 deathSkillEffect")]
    public string deathSkillId;

    private static readonly Dictionary<BuffInstance, Action> _handlers
        = new Dictionary<BuffInstance, Action>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var core = target.GetComponent<EnemyCore>();
        if (core == null) return;

        Action handler = null;
        handler = () =>
        {
            ExecuteDeathSkill(target);
            core.OnDied -= handler;
        };

        _handlers[buff] = handler;
        core.OnDied += handler;
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime) { }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (!_handlers.TryGetValue(buff, out var handler)) return;
        var core = target.GetComponent<EnemyCore>();
        if (core != null) core.OnDied -= handler;
        _handlers.Remove(buff);
    }

    private void ExecuteDeathSkill(GameObject caster)
    {
        if (caster == null) return;

        var core = caster.GetComponent<EnemyCore>();
        if (core == null) return;

        // 优先：调用角色身上携带的技能（从 EnemySkillManager 按 skillId 查找实例）
        if (!string.IsNullOrEmpty(deathSkillId))
        {
            if (core.SkillManager != null)
            {
                foreach (var inst in core.SkillManager.SkillInstances)
                {
                    if (inst == null || inst.Data == null) continue;
                    if (inst.Data.skillId != deathSkillId) continue;

                    // 直接执行效果（不检查冷却——亡语释放不应受冷却限制）
                    inst.ExecuteEffect(core, Vector2.up);
                    Debug.Log($"[DeathSkillTrigger] {caster.name} 死亡 → 调用身上技能 {deathSkillId}");
                    return;
                }
            }

            Debug.LogWarning($"[DeathSkillTrigger] {caster.name} 身上未找到技能 {deathSkillId}，回退到 deathSkillEffect");
        }

        // 回退：直接执行配置的效果
        if (deathSkillEffect == null) return;

        deathSkillEffect.Execute(
            core,
            Vector2.up,              // 方向无意义（WaveSkillEffect 以施法者为中心 360°）
            1f,                      // damageMultiplier
            core.GetOwnerType()
        );

        Debug.Log($"[DeathSkillTrigger] {caster.name} 死亡 → 执行 {deathSkillEffect.name}");
    }
}
