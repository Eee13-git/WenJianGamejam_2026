/// <summary>
/// 随从状态机：纯 C# 对象，由 EnemyFollower.Update() 驱动 Tick。
/// </summary>
public class FollowerStateMachine
{
    public FollowerStateBase CurrentState { get; private set; }

    public void ChangeState(FollowerStateBase newState)
    {
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState?.Enter();
    }

    public void Tick()
    {
        CurrentState?.Tick();
    }
}
