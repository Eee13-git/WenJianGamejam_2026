using UnityEngine;

/// <summary>
/// 攻击行为策略接口，允许不同攻击方式作为组件插入到敌人 Prefab。
/// </summary>
public interface IAttackBehavior
{
    /// <summary>尝试执行一次攻击，返回是否成功触发（用于AI调度）</summary>
    bool TryAttack(Transform target);
}
