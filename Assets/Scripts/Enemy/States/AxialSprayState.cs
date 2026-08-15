using UnityEngine;

/// <summary>
/// 三叉喷射状态（结核分枝杆菌等）— 进入攻击范围后沿三个固定角喷射：
/// 1) 进入状态即尝试释放三叉喷射技能（方向固定，不随玩家位置变化）；
/// 2) 喷射期间锁定移动（前摇 + 喷射全程原地不动，由 TriSprayRuntime 禁用 EnemyMovement）；
/// 3) 喷射结束后切回 ChaseState；技能冷却中则原地待命等待冷却。
/// </summary>
public class AxialSprayState : EnemyStateBase
{
    /// <summary>是否已触发喷射（进入喷射锁定阶段）</summary>
    private bool _spraying;

    public AxialSprayState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        Core.Movement?.Stop();
        _spraying = false;
    }

    public override void Tick()
    {
        if (Core.PlayerTarget == null || Core.Health == null)
        {
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        // ── 喷射锁定阶段：前摇 + 喷射全程原地不动 ──
        if (_spraying)
        {
            Core.Movement?.Stop();

            // 检测喷射是否结束（TriSprayRuntime 结束时会恢复 EnemyMovement）
            var runtime = Object.FindObjectOfType<TriSprayRuntime>();
            if (runtime == null || !runtime.IsActive)
            {
                Core.StateMachine.ChangeState(new ChaseState(Core));
            }
            return;
        }

        // ── 未喷射：尝试释放三叉喷射（方向固定，不依赖玩家位置） ──
        if (Core.SkillManager != null && Core.SkillManager.TryCastSkill(0, Core, Core.GetTargetDirection()))
        {
            _spraying = true;
        }
        // 技能冷却中则原地待命（保持不动，等待冷却）
        else
        {
            Core.Movement?.Stop();
        }
    }
}
