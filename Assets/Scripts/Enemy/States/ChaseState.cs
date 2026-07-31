using UnityEngine;

/// <summary>
/// 追击状态：加速追向玩家。进入攻击范围 → Attack；丢失视线 → Search；脱离探测范围 → Patrol/Idle。
/// </summary>
public class ChaseState : EnemyStateBase
{
    public ChaseState(EnemyCore core) : base(core) { }

    public override void Tick()
    {
        if (Core.PlayerTarget == null || Core.config == null)
        {
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        Vector2 playerPos = Core.PlayerTarget.position;
        float sqrDist = ((Vector2)Core.transform.position - playerPos).sqrMagnitude;
        float detectionSqr = Core.config.detectionRange * Core.config.detectionRange;
        float attackSqr = Core.config.attackRange * Core.config.attackRange;

        // 超出探测范围 → 返回巡逻/空闲
        if (sqrDist > detectionSqr)
        {
            Core.Movement?.Stop();
            if (Core.config.patrolPoints != null && Core.config.patrolPoints.Count > 0)
                Core.StateMachine.ChangeState(new PatrolState(Core));
            else
                Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        // 视线检测
        bool canSee = Core.Movement.HasLineOfSight(playerPos, Core.config.detectionRange);

        if (!canSee)
        {
            // 丢失视线 → 搜索
            Core.StateMachine.ChangeState(new SearchState(Core));
            return;
        }

        Core.LastKnownPlayerPosition = playerPos;

        // 进入攻击范围
        if (sqrDist <= attackSqr)
        {
            Core.StateMachine.ChangeState(new AttackState(Core));
            return;
        }

        // 追击移动（加速）
        if (Core.Movement != null)
        {
            Core.Movement.MoveSpeed = Core.config.chaseSpeed;
            Core.Movement.MoveTowardsPosition(playerPos);
        }
    }
}
