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

        // 轴向喷射型敌人（结核分枝杆菌）：进入攻击范围即进入喷射状态（沿三个固定角发射），而非继续追击
        if (Core.config != null && Core.config.useAxialSpray)
        {
            if (sqrDist <= attackSqr)
            {
                Core.StateMachine.ChangeState(new AxialSprayState(Core));
                return;
            }
        }

        // Boss 张嘴冲刺型（巨噬细胞 Boss）：同行/同列时进入冲刺状态
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

        // 进入攻击范围
        if (sqrDist <= attackSqr)
        {
            Core.StateMachine.ChangeState(new AttackState(Core));
            return;
        }

        // 追击（A* 寻路）
        var path = Core.Movement.GetPath(playerPos);
        Core.Movement?.MoveAlongPath(path, Core.Health.ChaseSpeed);
    }
}
