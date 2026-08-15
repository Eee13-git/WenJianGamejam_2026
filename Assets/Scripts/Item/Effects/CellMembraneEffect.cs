using UnityEngine;

/// <summary>
/// 细胞膜道具效果 — 受击后开启减伤窗口。
/// OnAcquire 给玩家添加 DamageReduction 组件，OnRemove 移除。
/// </summary>
[CreateAssetMenu(fileName = "CellMembraneEffect", menuName = "Game/Item Effect/Cell Membrane")]
public class CellMembraneEffect : ItemEffectBase
{
    [SerializeField] private float _reductionFactor = 0.3f;
    [SerializeField] private float _windowDuration = 1f;

    public override void OnAcquire(GameObject owner)
    {
        var dr = owner.GetComponent<DamageReduction>();
        if (dr == null)
            dr = owner.AddComponent<DamageReduction>();
        dr.ReductionFactor = _reductionFactor;
        dr.WindowDuration = _windowDuration;
    }

    public override void OnRemove(GameObject owner)
    {
        var dr = owner.GetComponent<DamageReduction>();
        if (dr != null)
            Object.Destroy(dr);
    }
}
