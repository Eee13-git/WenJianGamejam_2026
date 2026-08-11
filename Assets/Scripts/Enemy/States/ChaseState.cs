using UnityEngine;

/// <summary>
/// 追击状态：高速追向玩家。
/// 脱战距离 = 3× detectionRange，短暂失去视线切 SearchState。
/// 进入攻击范围 → AttackState。
/// </summary>
public class ChaseState : EnemyStateBase
{
    public ChaseState(EnemyCore core) : base(core) { }

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

        // 脱战判定：3× detectionRange
        float leashRange = Core.Health.DetectionRange * 3f;
        if (sqrDist > leashRange * leashRange)
        {
            Core.Movement?.Stop();
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        // 视线检测
        bool canSee = Core.Movement.HasLineOfSight(playerPos, Core.Health.DetectionRange);
        if (!canSee)
        {
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

        // 追击
        Core.Movement?.MoveTowardsPosition(playerPos, Core.Health.ChaseSpeed);
    }
}
