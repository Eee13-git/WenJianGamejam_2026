using UnityEngine;

/// <summary>
/// 巡逻状态：沿配置的 patrolPoints 逐个移动，到达后在每个点短暂停留。
/// 发现玩家 → Chase。
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
        _waitTimer = 0f;
    }

    public override void Tick()
    {
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

        if (Core.config == null || Core.config.patrolPoints == null || Core.config.patrolPoints.Count == 0)
        {
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        Vector2 target = Core.config.patrolPoints[_currentIndex];
        float sqrDist = ((Vector2)Core.transform.position - target).sqrMagnitude;

        if (sqrDist < ArriveThreshold * ArriveThreshold)
        {
            // 到达，等待
            Core.Movement?.Stop();
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= WaitPerPoint)
            {
                _waitTimer = 0f;
                _currentIndex = (_currentIndex + 1) % Core.config.patrolPoints.Count;
            }
        }
        else
        {
            _waitTimer = 0f;
            Core.Movement?.MoveTowardsPosition(target);
            // 巡逻用 patrolSpeed
            if (Core.Movement != null)
                Core.Movement.MoveSpeed = Core.config.patrolSpeed;
        }
    }
}
