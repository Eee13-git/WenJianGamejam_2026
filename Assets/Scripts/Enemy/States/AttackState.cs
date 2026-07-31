using UnityEngine;

/// <summary>
/// 攻击状态：停止移动，尝试用 IAttackBehavior 和技能攻击玩家。
/// 玩家离开攻击范围 → Chase。
/// </summary>
public class AttackState : EnemyStateBase
{
    public AttackState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        Core.Movement?.Stop();
    }

    public override void Tick()
    {
        if (Core.PlayerTarget == null || Core.config == null)
        {
            Core.StateMachine.ChangeState(new IdleState(Core));
            return;
        }

        Vector2 playerPos = Core.PlayerTarget.position;
        float sqrDist = ((Vector2)Core.transform.position - playerPos).sqrMagnitude;
        float attackSqr = Core.config.attackRange * Core.config.attackRange;

        // 玩家离开攻击范围 → 继续追击
        if (sqrDist > attackSqr * 1.2f)
        {
            Core.StateMachine.ChangeState(new ChaseState(Core));
            return;
        }

        // 尝试使用攻击行为
        Core.AttackBehavior?.TryAttack(Core.PlayerTarget);

        // 尝试释放技能
        if (Core.SkillManager != null && Core.SkillManager.SkillInstances.Count > 0)
        {
            Core.SkillManager.TryCastSkill(0, Core, Core.GetTargetDirection());
        }
    }
}
