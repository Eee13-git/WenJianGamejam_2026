using UnityEngine;

/// <summary>
/// 隐藏地图 Buff — 将小地图所有房间显示为未探索状态。
/// 通过 MinimapUI.HideMap 静态标志实现。OnApply 隐藏，OnRemove 恢复。
/// </summary>
[CreateAssetMenu(fileName = "HideMapBuff", menuName = "Game/Buff Effect/Hide Map")]
public class HideMapBuff : BuffEffectBase
{
    public override void OnApply(GameObject target, BuffInstance buff)
    {
        MinimapUI.HideMap = true;
        var minimap = FindObjectOfType<MinimapUI>(true);
        minimap?.Refresh();
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        MinimapUI.HideMap = false;
        var minimap = FindObjectOfType<MinimapUI>(true);
        minimap?.Refresh();
    }
}
