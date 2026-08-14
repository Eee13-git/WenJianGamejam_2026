using UnityEngine;

/// <summary>
/// 敌人减速 Buff — 通过 EnemyMovement.GlobalSpeedMultiplier 降低所有敌人移动速度。
/// 不影响子弹。OnApply 乘以减速倍率，OnRemove 除以恢复。
/// </summary>
[CreateAssetMenu(fileName = "EnemySlowBuff", menuName = "Game/Buff Effect/Enemy Slow")]
public class EnemySlowBuff : BuffEffectBase
{
    [Tooltip("减速倍率（0.7 = 30%减速）")]
    [SerializeField] private float _slowMultiplier = 0.7f;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        EnemyMovement.GlobalSpeedMultiplier *= _slowMultiplier;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        EnemyMovement.GlobalSpeedMultiplier /= _slowMultiplier;
    }
}
