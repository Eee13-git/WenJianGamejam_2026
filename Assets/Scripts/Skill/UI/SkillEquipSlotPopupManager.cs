using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能装配槽选择弹窗 —— 侵蚀·吞噬选定技能后，选择装配到哪个玩家技能槽。
/// 点击槽位：同技能=升级；异技能/空槽=替换/装备（EquipSkill 内部创建新实例）。
/// 关闭后回调 onClose（恢复时间）。
/// </summary>
public class SkillEquipSlotPopupManager : MonoBehaviour
{
    public static SkillEquipSlotPopupManager Instance { get; private set; }

    [Header("预制体")]
    [SerializeField] private SkillEquipSlotView _slotPrefab;

    [Header("弹窗 UI（预制体中预设）")]
    [SerializeField] private GameObject _backdrop;
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _cancelText;
    [SerializeField] private Button _cancelButton;

    [Header("悬停提示（运行时自动创建）")]
    [SerializeField] private GameObject _hoverTooltip;
    [SerializeField] private TMPro.TMP_Text _hoverName;
    [SerializeField] private TMPro.TMP_Text _hoverDesc;

    private Font _runtimeFont;

    private SkillData _skillData;
    private PlayerSkillManager _playerSkillManager;
    private Action _onClose;
    private bool _isShowing;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // 动态字体无法被预制体序列化，运行时创建（全局像素字体）
        _runtimeFont = Resources.Load<Font>("Fonts/ark-pixel-12px-monospaced-zh_cn");
        if (_runtimeFont == null)
            _runtimeFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

        if (_backdrop != null)
            _backdrop.SetActive(false);
        if (_popupPanel != null)
            _popupPanel.SetActive(false);

        // 运行时创建悬停 tooltip（弹窗提升为根画布后需独立于 UICanvas 显示）
        if (_hoverTooltip == null)
            _hoverTooltip = PopupTooltipBuilder.Create(transform, out _hoverName, out _hoverDesc);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 显示技能装配槽选择弹窗。
    /// </summary>
    /// <param name="skillData">玩家已选定要夺取的技能数据（可为玩家库之外的敌人技能）</param>
    /// <param name="player">玩家技能管理器</param>
    /// <param name="onClose">弹窗关闭回调（恢复时间等）</param>
    public void ShowPopup(SkillData skillData, PlayerSkillManager player, Action onClose)
    {
        if (_isShowing) return;
        _isShowing = true;

        _skillData = skillData;
        _playerSkillManager = player;
        _onClose = onClose;

        // 关键：提升为根画布（sortingOrder=100 只对根画布有效）。
        // 弹窗嵌套在 UICanvas 下时是子画布，sortingOrder 被忽略，
        // 会被拾取提示框（ItemUI/ItemDetailPopup，常驻屏幕中央且 raycastTarget=true）遮挡点击。
        if (transform.parent != null)
        {
            transform.SetParent(null);
            transform.SetAsLastSibling();
        }

        // 根画布必须为 ScreenSpaceOverlay 才铺满屏幕（prefab 序列化为 WorldSpace 以规避保存归零）
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (_popupPanel == null) { _isShowing = false; return; }

        if (_titleText != null)
        {
            _titleText.font = _runtimeFont;
            _titleText.text = "选择装配槽位";
        }
        if (_cancelText != null)
            _cancelText.font = _runtimeFont;

        // 清理旧槽位
        if (_slotContainer != null)
        {
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);
        }

        // 动态生成玩家技能槽
        int slotCount = player != null ? player.SlotCount : 0;
        var createdSlots = new System.Collections.Generic.List<RectTransform>();
        for (int i = 0; i < slotCount; i++)
        {
            int captured = i;
            SkillInstance current = player.GetSkill(i);
            string keyLabel = KeyCodeToString(player.GetKeyCode(i));

            if (_slotPrefab == null || _slotContainer == null) break;

            SkillEquipSlotView slotView = Instantiate(_slotPrefab, _slotContainer);
            slotView.Refresh(captured, keyLabel, current, _skillData.skillId);
            slotView.SetHoverHandler(ShowHoverTooltip, HideHoverTooltip);
            createdSlots.Add(slotView.transform as RectTransform);

            Button slotBtn = slotView.GetComponent<Button>();
            if (slotBtn == null) slotBtn = slotView.gameObject.AddComponent<Button>();
            slotBtn.onClick.RemoveAllListeners();
            slotBtn.onClick.AddListener(() => OnSlotSelected(captured));

            // 同技能已满级 → 不可再升级（置灰）
            slotBtn.interactable = slotView.ActionBadge != "已满";
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(ClosePopup);
        }

        if (_backdrop != null)
            _backdrop.SetActive(true);
        _popupPanel.SetActive(true);

        // 根据技能槽位数量自动调整弹窗大小（内容完整容纳 + 上下大量留白）
        AutoSizePopup(createdSlots);
    }

    /// <summary>
    /// 根据本次创建的槽位数量自动调整弹窗面板高度。
    /// 容器高度 = 实际内容高度；面板高度 = 内容高度 + 上下留白（标题/取消按钮/间距）。
    /// 只统计本次创建的槽位（避免旧槽位 Destroy 延迟残留导致尺寸误算）。
    /// </summary>
    private void AutoSizePopup(System.Collections.Generic.List<RectTransform> slots)
    {
        if (_slotContainer == null || _popupPanel == null) return;

        float contentH = 0f;
        var vlg = _slotContainer.GetComponent<VerticalLayoutGroup>();
        for (int i = 0; i < slots.Count; i++)
        {
            var child = slots[i];
            if (child == null) continue;
            contentH += child.rect.height;
            if (vlg != null && i < slots.Count - 1)
                contentH += vlg.spacing;
        }

        var containerRt = _slotContainer as RectTransform;
        containerRt.sizeDelta = new Vector2(containerRt.sizeDelta.x, contentH);
        var panelRt = _popupPanel.transform as RectTransform;
        // 面板高度 = 内容高度 + 留白。留白随槽位数量递增：基准220（顶部65+标题40+底部67+取消36+余量12）+ 每槽30
        panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, contentH + 220f + slots.Count * 30f);
    }

    // ==================== 悬停 tooltip ====================

    private void ShowHoverTooltip(SkillEquipSlotView slot)
    {
        if (slot == null) return;

        if (_hoverName != null) _hoverName.text = slot.DisplayName;
        if (_hoverDesc != null) _hoverDesc.text = slot.DisplayDesc;

        if (_hoverTooltip != null)
        {
            _hoverTooltip.SetActive(true);
            var rt = _hoverTooltip.GetComponent<RectTransform>();
            if (rt != null)
                rt.position = slot.transform.position + new Vector3(40f, 40f, 0f);
        }
    }

    private void HideHoverTooltip()
    {
        if (_hoverTooltip != null)
            _hoverTooltip.SetActive(false);
    }

    private void OnSlotSelected(int index)
    {
        if (_playerSkillManager == null || _skillData == null) return;

        SkillInstance current = _playerSkillManager.GetSkill(index);

        if (current != null && current.Data.skillId == _skillData.skillId)
        {
            // 同技能：升级（UpgradeSkill 直接操作槽内实例并触发事件，不管技能来源）
            _playerSkillManager.UpgradeSkill(index);
        }
        else
        {
            // 空槽装备 / 异技能替换：优先玩家库，没有则用敌人 SkillData 直接创建
            if (!_playerSkillManager.EquipSkill(index, _skillData.skillId))
                _playerSkillManager.EquipSkillData(index, _skillData);
        }

        ClosePopup();
    }

    public void ClosePopup()
    {
        if (!_isShowing) return;
        _isShowing = false;

        if (_backdrop != null)
            _backdrop.SetActive(false);
        if (_popupPanel != null)
            _popupPanel.SetActive(false);
        // 关闭时隐藏悬停 tooltip（避免替换技能后残留显示）
        if (_hoverTooltip != null)
            _hoverTooltip.SetActive(false);

        _onClose?.Invoke();
        _onClose = null;
    }

    private static string KeyCodeToString(KeyCode key)
    {
        string s = key.ToString();
        if (s.StartsWith("Alpha"))
            return s.Substring(5);
        return s;
    }
}
