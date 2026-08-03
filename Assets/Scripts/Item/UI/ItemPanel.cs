using UnityEngine;
using TMPro;

/// <summary>
/// 道具 UI 面板 — 左上角水平排列，透明背景，鼠标悬停显示详情。
/// 仅在有道具时显示槽位，空槽不留占位。
/// </summary>
public class ItemPanel : MonoBehaviour
{
    [Header("预制体与容器")]
    [SerializeField] private ItemSlotView _slotPrefab;
    [SerializeField] private Transform _container;

    [Header("悬停提示")]
    [SerializeField] private GameObject _hoverTooltip;
    [SerializeField] private TMP_Text _hoverName;
    [SerializeField] private TMP_Text _hoverDesc;

    [Header("布局")]
    [SerializeField] private int _maxSlots = 30;

    public static ItemPanel Instance { get; private set; }

    private ItemSlotView[] _views = System.Array.Empty<ItemSlotView>();

    private void Awake()
    {
        Instance = this;
        if (_hoverTooltip != null) _hoverTooltip.SetActive(false);
    }

    /// <summary>初始化，不预建空槽位（透明背景策略）</summary>
    public void Initialize(int slotCount)
    {
        int count = Mathf.Min(slotCount, _maxSlots);
        _views = new ItemSlotView[count];
    }

    public void RefreshSlot(int index, in ItemViewData data, ItemData boundItem)
    {
        if (index < 0 || index >= _maxSlots) return;

        // 按需扩容
        if (index >= _views.Length)
            ExpandSlots(index + 1);

        // 按需创建（懒加载）
        if (_views[index] == null)
        {
            var view = Instantiate(_slotPrefab, _container);
            view.name = $"ItemSlot_{index}";
            _views[index] = view;
        }

        _views[index].Refresh(data, boundItem);
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= _views.Length) return;
        if (_views[index] == null) return;

        SafeDestroy(_views[index].gameObject);
        _views[index] = null;
    }

    /// <summary>鼠标悬停到槽位时显示详情</summary>
    public void ShowHoverTooltip(ItemSlotView slot)
    {
        if (slot?.BoundItem == null) return;

        var item = slot.BoundItem;
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
            var rt = _hoverTooltip.GetComponent<RectTransform>();
            if (rt != null)
                rt.position = slot.transform.position + new Vector3(40f, 40f, 0f);
        }
    }

    /// <summary>鼠标离开时隐藏详情</summary>
    public void HideHoverTooltip()
    {
        if (_hoverTooltip != null)
            _hoverTooltip.SetActive(false);
    }

    private void ExpandSlots(int newSize)
    {
        int target = Mathf.Min(newSize, _maxSlots);
        if (target <= _views.Length) return;

        var newViews = new ItemSlotView[target];
        for (int i = 0; i < _views.Length; i++)
            newViews[i] = _views[i];
        _views = newViews;
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
