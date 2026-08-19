using UnityEngine;

/// <summary>
/// 搜索状态：移动到玩家最后已知位置，到达后等待 searchDuration 秒。
/// 重新发现玩家 → ChaseState；超时 → IdleState。
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
        _arrived   = false;
    }

    public override void Tick()
    {
        // 期间重新发现玩家 → 立即追击
        if (Core.PlayerTarget != null && Core.Health != null)
        {
            Vector2 playerPos = Core.PlayerTarget.position;
            if (Core.Movement.HasLineOfSight(playerPos, Core.Health.DetectionRange))
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
                _arrived   = true;
                _enterTime = Time.time;
                Core.Movement?.Stop();
            }
            else
            {
                // 搜索时用 patrolSpeed，额外减速 0.8（A* 寻路）
                var path = Core.Movement.GetPath(lastPos);
                Core.Movement?.MoveAlongPath(path,
                    Core.Health != null ? Core.Health.PatrolSpeed : 1f, 0.8f);
            }
        }
        else
        {
            Core.Movement?.Stop();
            if (Time.time - _enterTime > _searchDuration)
            {
                Core.StateMachine.ChangeState(new IdleState(Core));
            }
        }
    }
}
