using UnityEngine;

/// <summary>
/// 随从攻击状态：停止移动，朝敌人释放技能。
/// 敌人消失 → IdleState；离开攻击范围 → ChaseState。
/// </summary>
public class FollowerAttackState : FollowerStateBase
{
    public FollowerAttackState(EnemyFollower follower) : base(follower) { }

    public override void Enter()
    {
        Movement?.Stop();
    }

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

        // 离开攻击范围 → 追击
        if (dist > Follower.AttackRange * 1.2f)
        {
            Follower.StateMachine.ChangeState(new FollowerChaseState(Follower));
            return;
        }

        // 释放技能
        if (SkillManager != null && SkillManager.SkillInstances.Count > 0)
        {
            Vector2 dir = ((Vector2)enemy.position - pos).normalized;
            SkillManager.TryCastSkill(0, Core, dir);
        }
    }
}
