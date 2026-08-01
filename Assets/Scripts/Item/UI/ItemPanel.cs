using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具 UI 面板 — 管理 ItemSlotView 的创建与刷新。
/// 类似 SkillUIPanel 的职责，使用 ScrollRect 支持大量道具。
/// </summary>
public class ItemPanel : MonoBehaviour
{
    [Header("预制体与容器")]
    [SerializeField] private ItemSlotView _slotPrefab;
    [SerializeField] private Transform _container;

    [Header("滚动")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _viewport;

    [Header("布局")]
    [SerializeField] private int _maxSlots = 30;
    [SerializeField] private GridLayoutGroup _gridLayout;

    [Header("详情面板")]
    [SerializeField] private ItemDetailPanel _detailPanel;

    /// <summary>单例（供 ItemSlotView 回调）</summary>
    public static ItemPanel Instance { get; private set; }

    private ItemSlotView[] _views = System.Array.Empty<ItemSlotView>();
    private int _currentItemCount;

    private void Awake()
    {
        Instance = this;
    }

    public void Initialize(int slotCount)
    {
        int count = Mathf.Min(slotCount, _maxSlots);

        for (int i = _container.childCount - 1; i >= 0; i--)
            SafeDestroy(_container.GetChild(i).gameObject);

        _views = new ItemSlotView[count];
        _currentItemCount = 0;

        for (int i = 0; i < count; i++)
        {
            ItemSlotView view = Instantiate(_slotPrefab, _container);
            view.name = $"ItemSlot_{i}";
            view.SetEmpty();
            _views[i] = view;
        }

        if (_detailPanel != null)
            _detailPanel.gameObject.SetActive(false);
    }

    public void RefreshSlot(int index, in ItemViewData data, ItemData boundItem)
    {
        if (index < 0 || index >= _views.Length)
            ExpandSlots(index + 1);

        if (index < _views.Length)
        {
            _views[index]?.Refresh(data, boundItem);
            _currentItemCount = Mathf.Max(_currentItemCount, index + 1);
        }
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= _views.Length) return;
        _views[index]?.SetEmpty();
    }

    /// <summary>槽位被点击 — 显示详情</summary>
    public void OnSlotClicked(ItemSlotView slot)
    {
        if (slot == null || slot.BoundItem == null) return;
        if (_detailPanel != null)
        {
            _detailPanel.Show(slot.BoundItem);
        }
    }

    private void ExpandSlots(int newSize)
    {
        int target = Mathf.Min(newSize, _maxSlots);
        if (target <= _views.Length) return;

        var newViews = new ItemSlotView[target];
        for (int i = 0; i < _views.Length; i++)
            newViews[i] = _views[i];

        for (int i = _views.Length; i < target; i++)
        {
            ItemSlotView view = Instantiate(_slotPrefab, _container);
            view.name = $"ItemSlot_{i}";
            view.SetEmpty();
            newViews[i] = view;
        }

        _views = newViews;
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
