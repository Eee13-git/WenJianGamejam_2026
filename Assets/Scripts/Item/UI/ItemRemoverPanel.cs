using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道具移除面板 — 方格网格布局，复用 ItemSlotView 预制体。
/// 鼠标悬停显示道具描述，点击移除一个道具。
/// 由 ItemRemover 按下 F 键后打开。关闭时恢复游戏交互。
/// </summary>
public class ItemRemoverPanel : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _remainingText;
    [SerializeField] private Transform _gridContainer;
    [SerializeField] private ItemSlotView _slotPrefab;
    [SerializeField] private Button _closeButton;

    [Header("悬停提示")]
    [SerializeField] private GameObject _hoverTooltip;
    [SerializeField] private TMP_Text _hoverName;
    [SerializeField] private TMP_Text _hoverDesc;

    private ItemManager _itemManager;
    private int _remainingCount;      // -1 = 无限
    private Action _onPanelClosed;

    private readonly List<ItemSlotView> _slots = new List<ItemSlotView>();

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Close);
        if (_hoverTooltip != null) _hoverTooltip.SetActive(false);
        // 注意：不要在 Awake 里 SetActive(false)。
        // 该对象在场景中初始为 inactive，首次 Show() 的 SetActive(true)
        // 才会触发 Awake，若此处再 SetActive(false) 会导致首次面板不显示。
    }

    /// <summary>打开面板，显示玩家道具列表</summary>
    public void Show(ItemManager itemManager, int removeCount, Action onPanelClosed = null)
    {
        _itemManager = itemManager;
        _remainingCount = removeCount;
        _onPanelClosed = onPanelClosed;
        gameObject.SetActive(true);
        RefreshList();
    }

    /// <summary>关闭面板</summary>
    public void Close()
    {
        gameObject.SetActive(false);
        HideHoverTooltip();
        _onPanelClosed?.Invoke();
    }

    private void RefreshList()
    {
        // 清空旧槽位（立即销毁，避免同帧重建时旧槽位累积）
        foreach (var slot in _slots)
        {
            if (slot != null) DestroyImmediate(slot.gameObject);
        }
        _slots.Clear();

        // 更新标题和剩余次数
        if (_titleText != null)
            _titleText.text = "选择要移除的道具";
        if (_remainingText != null)
        {
            _remainingText.text = _remainingCount < 0
                ? "可移除次数: 无限"
                : _remainingCount > 0
                    ? $"剩余移除次数: {_remainingCount}"
                    : "移除次数已耗尽";
        }

        if (_itemManager == null || _itemManager.Count == 0)
        {
            if (_titleText != null)
                _titleText.text = "没有可移除的道具";
            return;
        }

        // 为每个道具创建方格槽位
        foreach (var item in _itemManager.Items)
        {
            if (item == null) continue;

            int count = _itemManager.GetItemCount(item.itemId);
            var slot = Instantiate(_slotPrefab, _gridContainer);
            var data = new ItemViewData
            {
                Icon = item.icon,
                Name = item.itemName,
                Description = item.description,
                Quality = item.quality,
                Count = count
            };
            slot.Refresh(data, item);

            // 绑定自定义 hover 回调
            slot.SetHoverHandler(
                s => ShowHoverTooltip(s.BoundItem),
                HideHoverTooltip);

            // 绑定点击移除（Button 组件已在 prefab 上）
            var button = slot.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                var capturedItem = item;
                button.onClick.AddListener(() => OnSlotClicked(capturedItem));
            }

            _slots.Add(slot);
        }
    }

    private void OnSlotClicked(ItemData item)
    {
        if (_itemManager == null) return;
        if (_remainingCount == 0) return;

        _itemManager.RemoveItem(item.itemId);

        if (_remainingCount > 0)
        {
            _remainingCount--;
            if (_remainingCount == 0)
            {
                Close();
                return;
            }
        }

        if (_itemManager.Count == 0)
        {
            Close();
            return;
        }

        RefreshList();
    }

    // ==================== 悬停提示 ====================

    private void ShowHoverTooltip(ItemData item)
    {
        if (item == null) return;

        if (_hoverName != null)
        {
            _hoverName.text = item.itemName;
            _hoverName.color = ItemPickup.QualityToColor(item.quality);
        }
        if (_hoverDesc != null)
            _hoverDesc.text = item.description;

        if (_hoverTooltip != null)
        {
            _hoverTooltip.SetActive(true);
            UpdateTooltipPosition();
        }
    }

    private void HideHoverTooltip()
    {
        if (_hoverTooltip != null)
            _hoverTooltip.SetActive(false);
    }

    private void UpdateTooltipPosition()
    {
        if (_hoverTooltip == null || !_hoverTooltip.activeSelf) return;
        var rt = _hoverTooltip.GetComponent<RectTransform>();
        if (rt != null)
            rt.position = Input.mousePosition + new Vector3(20f, -20f, 0f);
    }

    private void Update()
    {
        if (_hoverTooltip != null && _hoverTooltip.activeSelf)
            UpdateTooltipPosition();
    }
}
