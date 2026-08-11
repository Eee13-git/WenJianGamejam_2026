using UnityEngine;

/// <summary>
/// 追击状态：高速追向玩家。
/// 脱战判定用 3× detectionRange（而非 detectionRange）防止刚进入追击就退出。
/// 短暂失去视线不退出——只切 SearchState。
/// 进入攻击范围 → Attack。
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
        float attackSqr = Core.config.attackRange * Core.config.attackRange;

        // — 脱战判定：3× detectionRange，远大于初始发现距离 —
        float leashRange = Core.config.detectionRange * 3f;
        float leashSqr = leashRange * leashRange;

        if (sqrDist > leashSqr)
        {
            Core.Movement?.Stop();
            if (Core.config.patrolPoints != null && Core.config.patrolPoints.Count > 0)
                Core.StateMachine.ChangeState(new PatrolState(Core));
            else
                Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        // 视线检测（仅用作短暂丢失→搜索，不做退出）
        bool canSee = Core.Movement.HasLineOfSight(playerPos, Core.config.detectionRange);

        if (!canSee)
        {
            Core.StateMachine.ChangeState(new SearchState(Core));
            return;
        }

        Core.LastKnownPlayerPosition = playerPos;

        // 进入攻击范围 → 攻击
        if (sqrDist <= attackSqr)
        {
            Core.StateMachine.ChangeState(new AttackState(Core));
            return;
        }

        // 追击移动
        if (Core.Movement != null)
        {
            Core.Movement.MoveSpeed = Core.config.chaseSpeed;
            Core.Movement.MoveTowardsPosition(playerPos);
        }
    }
}
