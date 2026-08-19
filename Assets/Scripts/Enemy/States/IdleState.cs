using UnityEngine;

/// <summary>
/// 空闲状态：原地随机停留后上下/左右随机移动，循环往复。
/// 发现玩家 → ChaseState；有 patrolPoints → PatrolState。
/// </summary>
public class IdleState : EnemyStateBase
{
    private enum SubState { Wait, Wander }

    private SubState _subState;
    private float _waitTimer;
    private float _waitDuration;
    private Vector2 _wanderTarget;

    private static readonly float ArriveThreshold = 0.3f;

    public IdleState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        EnterWaitState();
    }

    public override void Tick()
    {
        // 索敌优先
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
        {
            Core.StateMachine.ChangeState(new PatrolState(Core));
            return;
        }

        // 子状态机：Wait → Wander → Wait
        switch (_subState)
        {
            case SubState.Wait:
                _waitTimer += Time.deltaTime;
                if (_waitTimer >= _waitDuration)
                    EnterWanderState();
                break;

            case SubState.Wander:
                float sqrDist = ((Vector2)Core.transform.position - _wanderTarget).sqrMagnitude;
                if (sqrDist < ArriveThreshold * ArriveThreshold)
                    EnterWaitState();
                else
                    Core.Movement?.MoveTowardsPosition(_wanderTarget, Core.Health.PatrolSpeed);
                break;
        }
    }

    private void EnterWaitState()
    {
        _subState = SubState.Wait;
        _waitTimer = 0f;
        var range = Core.config != null ? Core.config.idleWaitTimeRange : new Vector2(1f, 3f);
        _waitDuration = Random.Range(range.x, range.y);
        Core.Movement?.Stop();
    }

    private void EnterWanderState()
    {
        _subState = SubState.Wander;

        // 随机方向：上/下/左/右
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        Vector2 dir = dirs[Random.Range(0, 4)];

        // 随机距离
        var distRange = Core.config != null ? Core.config.idleWanderDistanceRange : new Vector2(1f, 3f);
        float dist = Random.Range(distRange.x, distRange.y);

        _wanderTarget = (Vector2)Core.transform.position + dir * dist;
    }

    private void TryFindPlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) Core.PlayerTarget = p.transform;
    }
}
