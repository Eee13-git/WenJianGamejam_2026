using UnityEngine;

/// <summary>
/// 随从待机状态：玩家附近原地待命。
/// 发现敌人 → ChaseState；玩家离开跟随距离 → FollowState。
/// </summary>
public class FollowerIdleState : FollowerStateBase
{
    public FollowerIdleState(EnemyFollower follower) : base(follower) { }

    public override void Enter()
    {
        Movement?.Stop();
    }

    public override void Tick()
    {
        Vector2 pos = Core.transform.position;

        // 搜索敌人 → 追击
        Transform enemy = Follower.FindNearestEnemy(pos);
        if (enemy != null)
        {
            Follower.StateMachine.ChangeState(new FollowerChaseState(Follower));
            return;
        }

        // 玩家离开跟随距离 → 跟随
        float dist = Vector2.Distance(pos, Player.position);
        if (dist > Follower.FollowDistance)
        {
            Follower.StateMachine.ChangeState(new FollowerFollowState(Follower));
        }
    }
}
