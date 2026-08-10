using UnityEngine;

/// <summary>
/// 急性谵妄 buff 效果：敌人不再以玩家为目标，随处乱走，不发射弹幕。
/// OnApply — 清除玩家目标、切换 ConfusedState（乱走）、禁用技能管理器；
/// OnRemove — 恢复技能管理器、重新定位玩家、切回 Idle/Patrol。
/// 泛用减益效果：可作为"混乱/迷惑/失智"类 debuff 的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "DeliriumBuff", menuName = "Game/Buff Effect/Delirium (Confusion)")]
public class DeliriumBuff : BuffEffectBase
{
    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var core = target.GetComponent<EnemyCore>();
        if (core == null || core.IsDead) return;

        // 已同化的随从不受影响（是自己人）
        if (core.IsAssimilated) return;

        // 不再以玩家为目标
        core.PlayerTarget = null;

        // 禁用技能（不发射弹幕）
        if (core.SkillManager != null)
            core.SkillManager.enabled = false;

        // 停止当前行为，切换到乱走状态
        if (core.StateMachine != null)
            core.StateMachine.ChangeState(new ConfusedState(core));
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        var core = target.GetComponent<EnemyCore>();
        if (core == null || core.IsDead) return;

        // 恢复技能
        if (core.SkillManager != null)
            core.SkillManager.enabled = true;

        // 重新定位玩家
        if (core.PlayerTarget == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) core.PlayerTarget = player.transform;
        }

        // 恢复正常 AI（有巡逻点则巡逻，否则空闲）
        if (core.StateMachine != null)
        {
            if (core.config != null && core.config.patrolPoints != null && core.config.patrolPoints.Count > 0)
                core.StateMachine.ChangeState(new PatrolState(core));
            else
                core.StateMachine.ChangeState(new IdleState(core));
        }
    }
}
