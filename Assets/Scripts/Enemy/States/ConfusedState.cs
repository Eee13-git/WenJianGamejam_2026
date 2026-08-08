using UnityEngine;

/// <summary>
/// 混乱状态：敌人随处乱走（随机方向漫步），不检测玩家、不攻击、不施放技能。
/// 由急性谵妄 debuff 施加，持续期间敌人完全失去攻击性。
/// </summary>
public class ConfusedState : EnemyStateBase
{
    private Vector2 _wanderDir;
    private float _changeTimer;
    private static readonly float ChangeInterval = 0.8f;   // 每隔多久换一次方向
    private static readonly float WanderDistance = 3f;      // 每次漫步的目标距离

    public ConfusedState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        _changeTimer = 0f;
        _wanderDir = Random.insideUnitCircle.normalized;
        if (Core.Movement != null)
            Core.Movement.MoveSpeed = Core.config != null ? Core.config.patrolSpeed : 1f;
    }

    public override void Tick()
    {
        // 定时更换随机方向（随处乱走）
        _changeTimer += Time.deltaTime;
        if (_changeTimer >= ChangeInterval)
        {
            _changeTimer = 0f;
            _wanderDir = Random.insideUnitCircle.normalized;
        }

        Vector2 target = (Vector2)Core.transform.position + _wanderDir * WanderDistance;
        Core.Movement?.MoveTowardsPosition(target);
    }
}
