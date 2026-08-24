using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 技能槽位 View —— 纯渲染，不引用任何游戏逻辑。
/// 由 Controller 调用 Refresh() 更新显示。鼠标悬停时弹出技能详情 tooltip。
/// </summary>
public class SkillSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("图标与文本")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _keyLabel;
    [SerializeField] private TMP_Text _levelText;

    [Header("冷却")]
    [SerializeField] private Image _cooldownOverlay;
    [SerializeField] private TMP_Text _cooldownText;

    [Header("状态遮罩")]
    [SerializeField] private GameObject _lockedOverlay;
    [SerializeField] private GameObject _emptyOverlay;

    [Header("交互")]
    [SerializeField] private Button _upgradeButton;

    /// <summary>悬停 tooltip 显示的技能名</summary>
    public string SkillName { get; private set; }
    /// <summary>悬停 tooltip 显示的技能描述</summary>
    public string SkillDescription { get; private set; }

    /// <summary>Controller 调用，传入数据刷新 UI</summary>
    public void Refresh(in SkillViewData data)
    {
        // 缓存悬停数据
        SkillName = data.SkillName;
        SkillDescription = data.Description;

        // 图标
        if (_icon != null)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;
        }

        // 快捷键
        if (_keyLabel != null)
        {
            _keyLabel.text = data.KeyLabel;
            _keyLabel.gameObject.SetActive(data.ShowKeyLabel);
        }

        // 等级
        if (_levelText != null)
            _levelText.text = data.IsEquipped ? $"Lv.{data.Level}" : "";

        // 冷却填充 — 仅在冷却中显示，否则完全隐藏
        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.fillAmount = data.CooldownPercent;
            _cooldownOverlay.enabled = data.IsCoolingDown && data.CooldownPercent > 0f;
        }

        // 冷却数字
        if (_cooldownText != null)
        {
            bool showCooldown = data.IsCoolingDown && data.CooldownRemaining > 0f;
            _cooldownText.gameObject.SetActive(showCooldown);
            if (showCooldown)
                _cooldownText.text = $"{data.CooldownRemaining:F1}s";
        }

        // 解锁/空槽遮罩
        if (_lockedOverlay != null)
            _lockedOverlay.SetActive(!data.IsUnlocked);
        if (_emptyOverlay != null)
            _emptyOverlay.SetActive(data.IsUnlocked && !data.IsEquipped);

        // 升级按钮 — 未装备或满级时隐藏
        if (_upgradeButton != null)
            _upgradeButton.gameObject.SetActive(data.IsEquipped && data.Level < data.MaxLevel);
    }

    // ==================== 鼠标悬停 ====================

    public void OnPointerEnter(PointerEventData eventData)
    {
        SkillUIPanel.Instance?.ShowHoverTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SkillUIPanel.Instance?.HideHoverTooltip();
    }
}
