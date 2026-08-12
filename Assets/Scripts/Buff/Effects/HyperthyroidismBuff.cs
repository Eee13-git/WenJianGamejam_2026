using UnityEngine;

/// <summary>
/// 甲亢 Buff — 子弹伤害和大小随飞行距离衰减。
/// 最近处3倍伤害，最远处0.1倍；大小从1.5倍衰减到0.3倍。永久 Buff。
/// </summary>
[CreateAssetMenu(fileName = "HyperthyroidismBuff", menuName = "Game/Buff Effect/Hyperthyroidism")]
public class HyperthyroidismBuff : BuffEffectBase
{
    [Header("衰减参数")]
    [SerializeField] private float _maxDamageMultiplier = 3f;
    [SerializeField] private float _minDamageMultiplier = 0.1f;
    [SerializeField] private float _maxScaleMultiplier = 1.5f;
    [SerializeField] private float _minScaleMultiplier = 0.3f;
    [Tooltip("从发射点到此距离达到最小值（世界单位）")]
    [SerializeField] private float _maxDistance = 8f;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        Projectile.DistanceDamageMode = true;
        Projectile.DistanceDamageMaxMult = _maxDamageMultiplier;
        Projectile.DistanceDamageMinMult = _minDamageMultiplier;
        Projectile.DistanceScaleMaxMult = _maxScaleMultiplier;
        Projectile.DistanceScaleMinMult = _minScaleMultiplier;
        Projectile.DistanceMaxRange = _maxDistance;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        Projectile.DistanceDamageMode = false;
    }
}
