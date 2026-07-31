using UnityEngine;

/// <summary>
/// 死亡状态：停止移动、禁用碰撞，延迟销毁 GameObject。
/// </summary>
public class DeadState : EnemyStateBase
{
    private float _destroyDelay = 0.5f;
    private float _deadTime;

    public DeadState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        _deadTime = Time.time;

        Core.Movement?.Stop();

        // 禁用所有 Collider2D 防止死亡后继续触发碰撞
        foreach (var col in Core.GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }
    }

    public override void Tick()
    {
        if (Time.time - _deadTime >= _destroyDelay)
        {
            Object.Destroy(Core.gameObject);
        }
    }
}
