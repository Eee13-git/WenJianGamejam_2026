using UnityEngine;

/// <summary>
/// 输入延迟 Buff — 永久生效，给玩家施加输入延迟。
/// 参数驱动，可被其他延迟类效果复用。
/// </summary>
[CreateAssetMenu(fileName = "InputDelayBuff", menuName = "Game/Buff Effect/Input Delay")]
public class InputDelayBuff : BuffEffectBase
{
    [SerializeField] private float _inputDelay = 0.05f;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        PlayerController.InputDelay = _inputDelay;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        PlayerController.InputDelay = 0f;
    }
}
