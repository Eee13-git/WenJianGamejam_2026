using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 侵蚀技能夺取弹窗 — 全局暂停时显示敌人技能列表，供玩家选择夺取/升级。
/// UI 结构由预制体预设；运行时只动态填充技能槽。
/// </summary>
public class SkillStealPopupManager : MonoBehaviour
{
    public static SkillStealPopupManager Instance { get; private set; }

    [Header("预制体")]
    [SerializeField] private SkillSlotView _slotPrefab;

    [Header("弹窗 UI（预制体中预设）")]
    [SerializeField] private GameObject _backdrop;
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _cancelText;
    [SerializeField] private Button _cancelButton;

    private Font _runtimeFont;
    private IReadOnlyList<SkillInstance> _enemySkills;
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
    }

    private void OnDestroy()
    {
        // 切换场景时清理单例
        if (Instance == this)
            Instance = null;
    }

    public void ShowPopup(IReadOnlyList<SkillInstance> enemySkills, PlayerSkillManager player, Action onClose)
    {
        if (_isShowing) return;
        _isShowing = true;

        _enemySkills = enemySkills;
        _playerSkillManager = player;
        _onClose = onClose;

        if (_popupPanel == null) { _isShowing = false; return; }

        if (_titleText != null)
        {
            _titleText.font = _runtimeFont;
            _titleText.text = "选择要夺取的技能";
        }

        if (_cancelText != null)
            _cancelText.font = _runtimeFont;

        // 清理旧槽位
        for (int i = _slotContainer.childCount - 1; i >= 0; i--)
            Destroy(_slotContainer.GetChild(i).gameObject);

        // 动态生成技能槽
        var createdSlots = new List<RectTransform>();
        for (int i = 0; i < enemySkills.Count; i++)
        {
            int captured = i;
            SkillInstance enemySkill = enemySkills[i];

            SkillSlotView slotView = Instantiate(_slotPrefab, _slotContainer);
            createdSlots.Add(slotView.transform as RectTransform);

            SkillViewData viewData = BuildSlotData(enemySkill);
            slotView.Refresh(viewData);

            // 点击 = 选择该技能
            Button slotBtn = slotView.GetComponent<Button>();
            if (slotBtn == null) slotBtn = slotView.gameObject.AddComponent<Button>();
            slotBtn.onClick.RemoveAllListeners();
            slotBtn.onClick.AddListener(() => OnSkillSelected(captured));
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(ClosePopup);
        }

        if (_backdrop != null)
            _backdrop.SetActive(true);
        _popupPanel.SetActive(true);

        // 根据技能数量自动调整弹窗大小（横向列表按列数自适应宽度）
        AutoSizePopup(createdSlots);
    }

    /// <summary>
    /// 根据本次创建的技能槽数量自动调整弹窗面板宽度（横向列表）。
    /// 容器宽度 = 实际内容宽度；面板宽度 = 内容宽度 + 左右留白。
    /// 只统计本次创建的槽位（避免旧槽位 Destroy 延迟残留导致尺寸误算）。
    /// </summary>
    private void AutoSizePopup(List<RectTransform> slots)
    {
        if (_slotContainer == null || _popupPanel == null) return;

        float contentW = 0f;
        var hlg = _slotContainer.GetComponent<HorizontalLayoutGroup>();
        for (int i = 0; i < slots.Count; i++)
        {
            var child = slots[i];
            if (child == null) continue;
            contentW += child.rect.width;
            if (hlg != null && i < slots.Count - 1)
                contentW += hlg.spacing;
        }

        var containerRt = _slotContainer as RectTransform;
        containerRt.sizeDelta = new Vector2(contentW, containerRt.sizeDelta.y);
        var panelRt = _popupPanel.transform as RectTransform;
        // 面板宽度 = 内容宽度 + 留白。留白随技能数量递增：基准160（左右各80）+ 每技能40
        panelRt.sizeDelta = new Vector2(contentW + 160f + slots.Count * 40f, panelRt.sizeDelta.y);
    }

    private SkillViewData BuildSlotData(SkillInstance skill)
    {
        SkillViewData data = default;
        if (skill == null || skill.Data == null) return data;

        SkillData def = skill.Data;
        data.Icon = def.icon;
        data.Level = skill.Level;
        data.MaxLevel = skill.MaxLevel;
        data.IsEquipped = true;
        data.IsUnlocked = true;
        data.IsCoolingDown = false;
        data.ShowKeyLabel = false;  // 弹窗中隐藏键位标签

        bool hasSkill = PlayerHasSkill(def.skillId, out int playerLv, out _);
        if (hasSkill)
            data.KeyLabel = playerLv < skill.MaxLevel ? "升级" : "已满";
        else
            data.KeyLabel = "夺取";

        return data;
    }

    private bool PlayerHasSkill(string skillId, out int level, out int slotIdx)
    {
        for (int i = 0; i < _playerSkillManager.SlotCount; i++)
        {
            SkillInstance skill = _playerSkillManager.GetSkill(i);
            if (skill != null && skill.Data.skillId == skillId)
            {
                level = skill.Level;
                slotIdx = i;
                return true;
            }
        }
        level = 0;
        slotIdx = -1;
        return false;
    }

    private void OnSkillSelected(int index)
    {
        if (index < 0 || index >= _enemySkills.Count) return;

        SkillInstance enemySkill = _enemySkills[index];
        SkillData skillData = enemySkill.Data;

        // 隐藏技能列表弹窗，进入装配槽选择
        HideUI();

        SkillEquipSlotPopupManager popup = SkillEquipSlotPopupManager.Instance;
        if (popup != null)
        {
            popup.ShowPopup(skillData, _playerSkillManager, Finish);
        }
        else
        {
            // 兜底：无弹窗时回退原有直接装配行为
            if (PlayerHasSkill(skillData.skillId, out int playerLv, out int slotIdx))
            {
                if (playerLv < enemySkill.MaxLevel)
                    _playerSkillManager.UpgradeSkill(slotIdx);
            }
            else
            {
                _playerSkillManager.AcquireSkill(skillData.skillId);
            }
            Finish();
        }
    }

    /// <summary>统一收尾：关闭回调（恢复时间）+ 复位状态</summary>
    private void Finish()
    {
        _onClose?.Invoke();
        _onClose = null;
        _isShowing = false;
    }

    /// <summary>隐藏弹窗 UI（不触发回调）</summary>
    private void HideUI()
    {
        if (_backdrop != null)
            _backdrop.SetActive(false);
        if (_popupPanel != null)
            _popupPanel.SetActive(false);
    }

    public void ClosePopup()
    {
        if (!_isShowing) return;
        _isShowing = false;

        HideUI();

        _onClose?.Invoke();
        _onClose = null;
    }
}
