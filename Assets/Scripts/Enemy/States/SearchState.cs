using UnityEngine;

/// <summary>
/// 搜索状态：移动到玩家最后已知位置，到达后等待 searchDuration 秒。
/// 重新发现玩家 → Chase；超时 → Patrol/Idle。
/// </summary>
public class SearchState : EnemyStateBase
{
    private float _enterTime;
    private float _searchDuration = 2.5f;
    private bool _arrived;
    private static readonly float ArriveThreshold = 0.4f;

    public SearchState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        _enterTime = Time.time;
        _arrived = false;
    }

    public override void Tick()
    {
        // 期间重新发现玩家 → 立即追击
        if (Core.PlayerTarget != null && Core.config != null)
        {
            Vector2 playerPos = Core.PlayerTarget.position;
            if (Core.Movement.HasLineOfSight(playerPos, Core.config.detectionRange))
            {
                Core.LastKnownPlayerPosition = playerPos;
                Core.StateMachine.ChangeState(new ChaseState(Core));
                return;
            }
        }

        Vector2 lastPos = Core.LastKnownPlayerPosition;
        float sqrDist = ((Vector2)Core.transform.position - lastPos).sqrMagnitude;

        if (!_arrived)
        {
            if (sqrDist < ArriveThreshold * ArriveThreshold)
            {
                _arrived = true;
                _enterTime = Time.time; // 重置计时，开始等待
                Core.Movement?.Stop();
            }
            else
            {
                Core.Movement?.MoveTowardsPosition(lastPos, 0.8f);
            }
        }
        else
        {
            Core.Movement?.Stop();
            if (Time.time - _enterTime > _searchDuration)
            {
                // 超时，返回巡逻或空闲
                if (Core.config != null && Core.config.patrolPoints != null && Core.config.patrolPoints.Count > 0)
                    Core.StateMachine.ChangeState(new PatrolState(Core));
                else
                    Core.StateMachine.ChangeState(new IdleState(Core));
            }
        }
    }
}
