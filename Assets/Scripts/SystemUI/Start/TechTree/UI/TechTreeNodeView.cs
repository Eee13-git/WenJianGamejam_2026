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

    // 状态颜色（灰青→暗青）
    // 普通节点
    private static readonly Color UnlockedColor = new(0.15f, 0.5f, 0.6f, 1f);
    private static readonly Color AvailableColor = new(0.25f, 0.6f, 0.4f, 1f);
    private static readonly Color LockedColor = new(0.35f, 0.4f, 0.45f, 1f);
    private static readonly Color UnlockedTextColor = new(0.6f, 1f, 1f);
    private static readonly Color AvailableTextColor = Color.white;
    private static readonly Color LockedTextColor = new(0.85f, 0.88f, 0.88f);

    // 边框颜色
    private static readonly Color UnlockedBorderColor = new(0.3f, 0.9f, 0.3f, 1f);
    private static readonly Color AvailableBorderColor = new(1f, 0.9f, 0.3f, 1f);
    private static readonly Color LockedBorderColor = new(0.5f, 0.5f, 0.55f, 1f);

    // 机制门颜色（紫色系）
    // 机制节点
    private static readonly Color MechUnlockedColor = new(0.4f, 0.2f, 0.7f, 1f);
    private static readonly Color MechAvailableColor = new(0.6f, 0.35f, 0.8f, 1f);
    private static readonly Color MechLockedColor = new(0.35f, 0.25f, 0.5f, 1f);
    private static readonly Color MechUnlockedBorder = new(0.6f, 0.3f, 0.9f, 1f);
    private static readonly Color MechAvailableBorder = new(0.8f, 0.5f, 1f, 1f);
    private static readonly Color MechLockedBorder = new(0.4f, 0.25f, 0.55f, 1f);

    // 随从/技能微强化颜色（灰青→暗青，与普通节点一致）
    private static readonly Color StatMechUnlockedColor = new(0.1f, 0.3f, 0.35f, 1f);
    private static readonly Color StatMechAvailableColor = new(0.2f, 0.35f, 0.4f, 1f);
    private static readonly Color StatMechLockedColor = new(0.3f, 0.4f, 0.42f, 1f);
    private static readonly Color StatMechUnlockedBorder = new(0.3f, 0.9f, 0.3f, 1f);
    private static readonly Color StatMechAvailableBorder = new(1f, 0.9f, 0.3f, 1f);
    private static readonly Color StatMechLockedBorder = new(0.5f, 0.5f, 0.55f, 1f);

    // 效果文字颜色：未点亮=黄色，已点亮=绿色
    private static readonly Color EffectUnlockedColor = new(0.2f, 1f, 0.3f);
    private static readonly Color EffectLockedColor = new(1f, 0.9f, 0.1f);

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
                : (data.IsUnlocked ? EffectUnlockedColor : EffectLockedColor);
        }

        Color bgColor, textColor, borderColor;
        bool isMech = data.Type == TechTreeNodeViewData.NodeType.Mechanism;
        bool isStatMech = data.Type == TechTreeNodeViewData.NodeType.StatMechanism;

        if (data.IsUnlocked)
        {
            bgColor = isMech ? MechUnlockedColor : isStatMech ? StatMechUnlockedColor : UnlockedColor;
            textColor = UnlockedTextColor;
            borderColor = isMech ? MechUnlockedBorder : isStatMech ? StatMechUnlockedBorder : UnlockedBorderColor;
            if (_costText != null)
                _costText.text = "已解锁";
            if (_button != null)
                _button.interactable = false;
        }
        else if (data.CanUnlock)
        {
            bgColor = isMech ? MechAvailableColor : isStatMech ? StatMechAvailableColor : AvailableColor;
            textColor = AvailableTextColor;
            borderColor = isMech ? MechAvailableBorder : isStatMech ? StatMechAvailableBorder : AvailableBorderColor;
            if (_costText != null)
                _costText.text = $"消耗 {data.Cost} 点";
            if (_button != null)
                _button.interactable = true;
        }
        else
        {
            bgColor = isMech ? MechLockedColor : isStatMech ? StatMechLockedColor : LockedColor;
            textColor = LockedTextColor;
            borderColor = isMech ? MechLockedBorder : isStatMech ? StatMechLockedBorder : LockedBorderColor;
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
