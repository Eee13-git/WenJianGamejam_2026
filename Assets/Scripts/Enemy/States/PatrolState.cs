using UnityEngine;

/// <summary>
/// 巡逻状态：沿 patrolPoints 逐个移动，到达后短暂停留。
/// 发现玩家 → ChaseState。
/// </summary>
public class PatrolState : EnemyStateBase
{
    private int _currentIndex;
    private float _waitTimer;
    private static readonly float WaitPerPoint = 0.8f;
    private static readonly float ArriveThreshold = 0.3f;

    public PatrolState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        _currentIndex = 0;
        _waitTimer   = 0f;
    }

    public override void Tick()
    {
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

        var pts = Core.config != null ? Core.config.patrolPoints : null;
        if (pts == null || pts.Count == 0)
        {
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        Vector2 target = pts[_currentIndex];
        float sqrDist = ((Vector2)Core.transform.position - target).sqrMagnitude;

        if (sqrDist < ArriveThreshold * ArriveThreshold)
        {
            Core.Movement?.Stop();
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= WaitPerPoint)
            {
                _waitTimer = 0f;
                _currentIndex = (_currentIndex + 1) % pts.Count;
            }
        }
        else
        {
            _waitTimer = 0f;
            // 检测前方 0.5 格是否有障碍/墙，有则跳过当前点去下一个
            Vector2 moveDir = (target - (Vector2)Core.transform.position).normalized;
            if (Core.Movement != null && Core.Movement.IsDirectionBlocked(moveDir))
                _currentIndex = (_currentIndex + 1) % pts.Count;
            else
                Core.Movement?.MoveTowardsPosition(target, Core.Health.PatrolSpeed);
        }
    }
}
