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

        // 保持靠近
        Core.Movement?.MoveTowardsPosition(playerPos, Core.Health.ChaseSpeed, 0.3f);

        // 释放技能
        if (Core.SkillManager != null && Core.SkillManager.SkillInstances.Count > 0)
        {
            Core.SkillManager.TryCastSkill(0, Core, Core.GetTargetDirection());
        }
    }
}
