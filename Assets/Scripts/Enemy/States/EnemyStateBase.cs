using UnityEngine;

/// <summary>
/// 抽象状态基类。每个具体状态应继承该类并实现 Enter/Update/Exit。
/// 状态持有对 EnemyCore 的引用以便控制敌人行为。
/// </summary>
public abstract class EnemyStateBase
{
    protected EnemyCore Core { get; private set; }

    public EnemyStateBase(EnemyCore core)
    {
        Core = core;
    }

    /// <summary>进入状态时调用一次</summary>
    public virtual void Enter() { }

    /// <summary>每帧由状态机调用</summary>
    public virtual void Tick() { }

    /// <summary>退出状态时调用一次</summary>
    public virtual void Exit() { }
}
