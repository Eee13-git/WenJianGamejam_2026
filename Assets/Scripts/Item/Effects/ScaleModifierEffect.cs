using UnityEngine;

/// <summary>
/// 角色视觉缩放道具效果 — 修改 owner 的 transform.localScale。
/// 支持加算（flatBonus）和乘算（isMultiplicative）。
/// OnAcquire 放大，OnRemove 还原。天然支持叠加。
/// </summary>
[CreateAssetMenu(fileName = "ScaleModifierEffect", menuName = "Game/Item Effect/Scale Modifier")]
public class ScaleModifierEffect : ItemEffectBase
{
    [Tooltip("加算模式下每次增减的值（0.1 = +10% 体积）")]
    [SerializeField] private float _flatBonus = 0.1f;

    [Tooltip("乘算模式下的缩放倍率（1.1 = +10% 体积）")]
    [SerializeField] private float _percentBonus = 1.1f;

    [Tooltip("true=乘算(percentBonus), false=加算(flatBonus)")]
    [SerializeField] private bool _isMultiplicative = false;

    public override void OnAcquire(GameObject owner)
    {
        ApplyScale(owner, true);
    }

    public override void OnRemove(GameObject owner)
    {
        ApplyScale(owner, false);
    }

    private void ApplyScale(GameObject owner, bool acquire)
    {
        var t = owner.transform;
        Vector3 scale = t.localScale;

        if (_isMultiplicative)
        {
            if (acquire)
                scale *= _percentBonus;
            else
                scale /= _percentBonus;
        }
        else
        {
            float delta = acquire ? _flatBonus : -_flatBonus;
            scale += new Vector3(delta, delta, 0f);
        }

        // 防止缩为负数
        scale.x = Mathf.Max(scale.x, 0.1f);
        scale.y = Mathf.Max(scale.y, 0.1f);

        t.localScale = scale;
    }
}
