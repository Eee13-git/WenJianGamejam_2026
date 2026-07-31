using System;
using UnityEngine;

/// <summary>
/// 轻量状态机组件：管理当前状态的切换和每帧 Tick 调用。
/// 状态对象由外部创建并传入 ChangeState。
/// </summary>
public class EnemyStateMachine : MonoBehaviour
{
    public EnemyStateBase CurrentState { get; private set; }

    /// <summary>状态变更事件：参数 (oldState, newState)</summary>
    public event Action<EnemyStateBase, EnemyStateBase> OnStateChanged;//暂时没用，留给之后设计动画切换，音效，ui指示器之类的

    public void ChangeState(EnemyStateBase newState)
    {
        if (CurrentState != null)
            CurrentState.Exit();

        var old = CurrentState;
        CurrentState = newState;

        if (CurrentState != null)
            CurrentState.Enter();

        OnStateChanged?.Invoke(old, CurrentState);
    }

    private void Update()
    {
        CurrentState?.Tick();
    }
}
