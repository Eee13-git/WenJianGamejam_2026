using UnityEngine;

/// <summary>
/// 随从跟随状态：朝玩家移动。
/// 发现敌人 → ChaseState；接近玩家 → IdleState。
/// </summary>
public class FollowerFollowState : FollowerStateBase
{
    public FollowerFollowState(EnemyFollower follower) : base(follower) { }

    public override void Tick()
    {
        // 搜索敌人 → 追击
        Vector2 pos = Core.transform.position;
        Transform enemy = Follower.FindNearestEnemy(pos);
        if (enemy != null)
        {
            Follower.StateMachine.ChangeState(new FollowerChaseState(Follower));
            return;
        }

        // 接近玩家 → 待机
        float dist = Vector2.Distance(pos, Player.position);
        if (dist <= Follower.FollowDistance)
        {
            Follower.StateMachine.ChangeState(new FollowerIdleState(Follower));
            return;
        }

        // 朝玩家移动（A* 寻路）
        var path = Movement.GetPath(Player.position);
        Movement?.MoveAlongPath(path, Follower.FollowSpeed);
    }
}
