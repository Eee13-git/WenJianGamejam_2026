using UnityEngine;

/// <summary>
/// 攻击状态：停止移动，用技能攻击玩家。
/// 玩家离开攻击范围 → ChaseState。
/// </summary>
public class AttackState : EnemyStateBase
{
    public AttackState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        Core.Movement?.Stop();
    }

    public override void Tick()
    {
        if (Core.PlayerTarget == null || Core.Health == null)
        {
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        Vector2 playerPos = Core.PlayerTarget.position;
        float sqrDist = ((Vector2)Core.transform.position - playerPos).sqrMagnitude;
        float attackSqr = Core.Health.AttackRange * Core.Health.AttackRange;

        // 玩家离开攻击范围 → 追击
        if (sqrDist > attackSqr * 1.2f)
        {
            Core.StateMachine.ChangeState(new ChaseState(Core));
            return;
        }

        // Boss 张嘴冲刺型：检测同行/同列对齐 → 切 BossLungeState
        if (Core.config != null && Core.config.useBossLunge && Core.GetComponent<BossCore>() != null)
        {
            float threshold = Core.config.axialAlignThreshold > 0f ? Core.config.axialAlignThreshold : 0.6f;
            bool aligned = Mathf.Abs(playerPos.x - Core.transform.position.x) <= threshold
                        || Mathf.Abs(playerPos.y - Core.transform.position.y) <= threshold;
            if (aligned)
            {
                Core.StateMachine.ChangeState(new BossLungeState(Core));
                return;
            }
        }

        // 保持靠近
        Core.Movement?.MoveTowardsPosition(playerPos, Core.Health.ChaseSpeed, 0.3f);

        // 释放技能：支持随机选择就绪技能（多技能敌人概率发动）
        if (Core.SkillManager != null && Core.SkillManager.SkillInstances.Count > 0)
        {
            // Boss 张嘴冲刺型：lunge 由 BossLungeState 管理，AttackState 不施放技能
            // （BossCore.Update 负责施放非 lunge 技能如召唤）
            if (Core.config != null && Core.config.useBossLunge)
                return;

            if (Core.config != null && Core.config.useRandomSkill)
                Core.SkillManager.TryCastRandomReadySkill(Core, Core.GetTargetDirection());
            else
                Core.SkillManager.TryCastSkill(0, Core, Core.GetTargetDirection());
        }
    }
}
