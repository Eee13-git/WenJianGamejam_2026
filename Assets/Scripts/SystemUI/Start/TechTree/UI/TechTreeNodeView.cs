using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 科技树节点视图 — 纯渲染组件，类似 SkillSlotView。
/// 不包含任何游戏逻辑，仅接受 TechTreeNodeViewData 进行显示。
/// 预制体上挂载此组件，UI 元素通过 [SerializeField] 引用。
/// </summary>
public class TechTreeNodeView : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private Image _background;
    [SerializeField] private Image _border;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _effectText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _button;

    // 状态颜色
    private static readonly Color UnlockedColor = new(0.2f, 0.6f, 0.2f, 1f);
    private static readonly Color AvailableColor = new(0.85f, 0.75f, 0.2f, 1f);
    private static readonly Color LockedColor = new(0.45f, 0.45f, 0.5f, 1f);
    private static readonly Color UnlockedTextColor = new(0.85f, 1f, 0.85f);
    private static readonly Color AvailableTextColor = Color.white;
    private static readonly Color LockedTextColor = new(0.75f, 0.75f, 0.8f);

    // 边框颜色
    private static readonly Color UnlockedBorderColor = new(0.3f, 0.9f, 0.3f, 1f);
    private static readonly Color AvailableBorderColor = new(1f, 0.9f, 0.3f, 1f);
    private static readonly Color LockedBorderColor = new(0.5f, 0.5f, 0.55f, 0.5f);

    // 效果文字颜色（正数=绿色，负数=红色）
    private static readonly Color PositiveEffectColor = new(0.4f, 1f, 0.4f);
    private static readonly Color NegativeEffectColor = new(1f, 0.4f, 0.4f);

    private TechTreeNodeViewData _data;
    private System.Action<string> _onClick;

    /// <summary>配置节点数据和点击回调</summary>
    public void Configure(TechTreeNodeViewData data, System.Action<string> onClick)
    {
        _data = data;
        _onClick = onClick;

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClick);
        }
    }

    /// <summary>刷新显示（纯渲染，不包含逻辑）</summary>
    public void Refresh(in TechTreeNodeViewData data)
    {
        _data = data;

        if (_nameText != null)
            _nameText.text = data.DisplayName;
        if (_effectText != null)
        {
            _effectText.text = data.EffectSummary;
            _effectText.color = string.IsNullOrEmpty(data.EffectSummary) ? Color.white
                : (data.EffectSummary.StartsWith("+") ? PositiveEffectColor : NegativeEffectColor);
        }

        Color bgColor, textColor, borderColor;
        if (data.IsUnlocked)
        {
            bgColor = UnlockedColor;
            textColor = UnlockedTextColor;
            borderColor = UnlockedBorderColor;
            if (_costText != null)
                _costText.text = "已解锁";
            if (_button != null)
                _button.interactable = false;
        }
        else if (data.CanUnlock)
        {
            bgColor = AvailableColor;
            textColor = AvailableTextColor;
            borderColor = AvailableBorderColor;
            if (_costText != null)
                _costText.text = $"消耗 {data.Cost} 点";
            if (_button != null)
                _button.interactable = true;
        }
        else
        {
            bgColor = LockedColor;
            textColor = LockedTextColor;
            borderColor = LockedBorderColor;
            if (_costText != null)
            {
                _costText.text = data.CurrentTechPoints < data.Cost
                    ? $"需 {data.Cost} 点"
                    : "前置未满足";
            }
            if (_button != null)
                _button.interactable = false;
        }

        if (_background != null)
            _background.color = bgColor;
        if (_border != null)
            _border.color = borderColor;
        if (_nameText != null)
            _nameText.color = textColor;
        if (_costText != null)
            _costText.color = textColor;
    }

    /// <summary>获取当前节点ID</summary>
    public string NodeId => _data.NodeId;

    private void OnClick()
    {
        _onClick?.Invoke(_data.NodeId);
    }
}
