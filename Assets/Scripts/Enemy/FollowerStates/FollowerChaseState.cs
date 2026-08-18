using UnityEngine;

/// <summary>
/// 随从追击状态：朝最近敌人移动。
/// 敌人消失 → IdleState；进入攻击范围 → AttackState。
/// </summary>
public class FollowerChaseState : FollowerStateBase
{
    public FollowerChaseState(EnemyFollower follower) : base(follower) { }

    public override void Tick()
    {
        Vector2 pos = Core.transform.position;
        Transform enemy = Follower.FindNearestEnemy(pos);

        // 敌人消失 → 待机
        if (enemy == null)
        {
            Follower.StateMachine.ChangeState(new FollowerIdleState(Follower));
            return;
        }

        float dist = Vector2.Distance(pos, enemy.position);

        // 进入攻击范围 → 攻击
        if (dist <= Follower.AttackRange)
        {
            Follower.StateMachine.ChangeState(new FollowerAttackState(Follower));
            return;
        }

        // 追击敌人
        Movement?.MoveTowardsPosition(enemy.position, Follower.FollowSpeed);
    }
}
