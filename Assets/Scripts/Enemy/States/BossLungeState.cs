using UnityEngine;

/// <summary>
/// Boss 张嘴冲刺状态（巨噬细胞 Boss 等）— 检测玩家是否在同一行/同一列：
/// 1) 对齐（同行 |dy| 或同列 |dx| 小于阈值）→ 朝该轴方向释放冲刺技能（SlideSkillEffect 冲撞）；
/// 2) 冲刺期间由 SlideRuntime 处理位移与碰撞；
/// 3) 冲刺结束后切回 ChaseState；不对齐则切回 ChaseState 追击。
/// 与 AxialSprayState（原地喷射）不同：本状态朝轴向冲刺移动。
/// </summary>
public class BossLungeState : EnemyStateBase
{
    /// <summary>是否已触发冲刺</summary>
    private bool _lunging;

    /// <summary>冲刺是否已结束（检测 SlideRuntime 从 active 恢复）</summary>
    private bool _sawSlideEnd;

    public BossLungeState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        Core.Movement?.Stop();
        _lunging = false;
        _sawSlideEnd = false;
    }

    public override void Tick()
    {
        if (Core.PlayerTarget == null || Core.Health == null)
        {
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        // ── 冲刺锁定阶段 ──
        if (_lunging)
        {
            var slide = Core.GetComponent<SlideRuntime>();
            if (slide != null && slide.IsActive)
            {
                _sawSlideEnd = true;   // 冲刺中
            }
            else if (_sawSlideEnd)
            {
                // 冲刺已结束
                Core.StateMachine.ChangeState(new ChaseState(Core));
                return;
            }
            // 前摇阶段：SlideRuntime 尚未激活，继续等待
            return;
        }

        // ── 检测同行/同列对齐 ──
        Vector2 playerPos = Core.PlayerTarget.position;
        Vector2 myPos = Core.transform.position;
        float threshold = Core.config != null && Core.config.axialAlignThreshold > 0f
            ? Core.config.axialAlignThreshold
            : 0.6f;

        bool aligned = Mathf.Abs(playerPos.x - myPos.x) <= threshold
                    || Mathf.Abs(playerPos.y - myPos.y) <= threshold;

        if (!aligned)
        {
            Core.StateMachine.ChangeState(new ChaseState(Core));
            return;
        }

        // ── 对齐 → 尝试冲刺（朝玩家所在轴向，纯水平/垂直） ──
        // 计算轴向方向：同行→水平冲刺，同列→垂直冲刺
        float dx = playerPos.x - myPos.x;
        float dy = playerPos.y - myPos.y;
        Vector2 lungeDir;
        if (Mathf.Abs(dx) <= threshold)
        {
            // 同列 → 垂直冲刺
            lungeDir = new Vector2(0f, Mathf.Sign(dy));
        }
        else
        {
            // 同行 → 水平冲刺
            lungeDir = new Vector2(Mathf.Sign(dx), 0f);
        }

        if (Core.SkillManager != null && Core.SkillManager.TryCastSkill(0, Core, lungeDir))
        {
            _lunging = true;
        }
        // 技能冷却中则待命（保持对齐，不移动）
        else
        {
            Core.Movement?.Stop();
        }
    }
}
