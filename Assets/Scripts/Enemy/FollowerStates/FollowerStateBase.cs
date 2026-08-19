using UnityEngine;

/// <summary>
/// 随从状态基类。每个具体状态继承该类并实现 Enter/Tick/Exit。
/// </summary>
public abstract class FollowerStateBase
{
    protected EnemyFollower Follower { get; private set; }
    protected EnemyCore Core => Follower.Core;
    protected EnemyMovement Movement => Follower.Movement;
    protected EnemySkillManager SkillManager => Follower.SkillManager;
    protected Transform Player => Follower.Player;

    public FollowerStateBase(EnemyFollower follower)
    {
        Follower = follower;
    }

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}
