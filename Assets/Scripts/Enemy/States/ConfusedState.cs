using UnityEngine;

/// <summary>
/// 混乱状态：敌人随处乱走（随机方向漫步），不检测玩家、不攻击、不施放技能。
/// 由急性谵妄 debuff 施加。
/// </summary>
public class ConfusedState : EnemyStateBase
{
    private Vector2 _wanderDir;
    private float _changeTimer;
    private static readonly float ChangeInterval = 0.8f;
    private static readonly float WanderDistance = 3f;

    public ConfusedState(EnemyCore core) : base(core) { }

    public override void Enter()
    {
        _changeTimer = 0f;
        _wanderDir = Random.insideUnitCircle.normalized;
    }

    public override void Tick()
    {
        _changeTimer += Time.deltaTime;
        if (_changeTimer >= ChangeInterval)
        {
            _changeTimer = 0f;
            _wanderDir = Random.insideUnitCircle.normalized;
        }

        Vector2 target = (Vector2)Core.transform.position + _wanderDir * WanderDistance;
        float speed = Core.Health != null ? Core.Health.PatrolSpeed : 1f;
        Core.Movement?.MoveTowardsPosition(target, speed);
    }
}
