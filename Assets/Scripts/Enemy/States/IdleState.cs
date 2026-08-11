using UnityEngine;

/// <summary>
/// 空闲状态：原地待命，定期检测玩家。
/// 发现玩家 → ChaseState；有 patrolPoints → PatrolState。
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
        if (Core.PlayerTarget == null)
        {
            TryFindPlayer();
            return;
        }
        if (Core.Health == null) return;

        Vector2 playerPos = Core.PlayerTarget.position;
        bool canSee = Core.Movement.HasLineOfSight(playerPos, Core.Health.DetectionRange);

        if (canSee)
        {
            Core.LastKnownPlayerPosition = playerPos;
            Core.StateMachine.ChangeState(new ChaseState(Core));
            return;
        }

        var pts = Core.config != null ? Core.config.patrolPoints : null;
        if (pts != null && pts.Count > 0)
            Core.StateMachine.ChangeState(new PatrolState(Core));
    }

    private void TryFindPlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) Core.PlayerTarget = p.transform;
    }
}
