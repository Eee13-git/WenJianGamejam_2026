using UnityEngine;

/// <summary>
/// 空闲状态：原地待命，定期检测玩家。发现玩家 → Chase；有巡逻点 → Patrol。
/// </summary>
public class IdleState : EnemyStateBase
{
    public IdleState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        Core.Movement?.Stop();
    }

    public override void Tick()
    {
        // 懒加载玩家（EnemyCore 可能还没找到）
        if (Core.PlayerTarget == null)
        {
            TryFindPlayer();
            return;
        }
        if (Core.config == null) return;

        // 检测玩家
        Vector2 playerPos = Core.PlayerTarget.position;
        bool canSee = Core.Movement.HasLineOfSight(playerPos, Core.config.detectionRange);

        if (canSee)
        {
            Core.LastKnownPlayerPosition = playerPos;
            Core.StateMachine.ChangeState(new ChaseState(Core));
            return;
        }

        // 有巡逻点则转入巡逻
        if (Core.config.patrolPoints != null && Core.config.patrolPoints.Count > 0)
        {
            Core.StateMachine.ChangeState(new PatrolState(Core));
            return;
        }
    }

    private void TryFindPlayer()
    {
        if (UnityEngine.GameObject.FindGameObjectWithTag("Player") is GameObject p)
            Core.PlayerTarget = p.transform;
    }
}
