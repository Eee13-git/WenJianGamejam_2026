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

    [Header("多行布局")]
    [Tooltip("每行最多槽位数（到达后自动换行）")]
    [SerializeField] private int _columns = 18;
    [Tooltip("每行高度（含间距），用于多行时下移下方 UI")]
    [SerializeField] private float _rowHeight = 68f;
    [Tooltip("物品栏多行时需同步下移的下方 UI 元素（CurrencyPanel/StatsPanel/FollowerPanel）")]
    [SerializeField] private RectTransform[] _belowElements;

    public static ItemPanel Instance { get; private set; }

    private ItemSlotView[] _views = System.Array.Empty<ItemSlotView>();
    private float[] _belowBaseY;
    private int _currentRows = 1;

    private void Awake()
    {
        Instance = this;
        if (_hoverTooltip != null) _hoverTooltip.SetActive(false);

        // 记录下方元素基准 Y（单行时的位置）
        if (_belowElements != null)
        {
            _belowBaseY = new float[_belowElements.Length];
            for (int i = 0; i < _belowElements.Length; i++)
                _belowBaseY[i] = _belowElements[i] != null ? _belowElements[i].anchoredPosition.y : 0f;
        }
    }

    /// <summary>
    /// 根据占用槽位数更新布局：行数 = ceil(占用数 / 每行槽位数)，容器高度随行数增长，
    /// 下方 UI（属性栏/货币/随从栏）整体下移避免遮挡。
    /// </summary>
    private void UpdateLayout()
    {
        int count = 0;
        for (int i = 0; i < _views.Length; i++)
            if (_views[i] != null) count++;

        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Mathf.Max(1, _columns)));
        if (rows == _currentRows) return;
        _currentRows = rows;

        // 容器高度 = 行数 × 行高 + 上下内边距
        var crt = _container as RectTransform;
        if (crt != null)
            crt.sizeDelta = new Vector2(crt.sizeDelta.x, rows * _rowHeight + 8f);

        // 下方 UI 同步下移（每多一行下移一行高）
        if (_belowElements != null && _belowBaseY != null)
        {
            float shift = (rows - 1) * _rowHeight;
            for (int i = 0; i < _belowElements.Length; i++)
            {
                if (_belowElements[i] == null || _belowBaseY.Length <= i) continue;
                var pos = _belowElements[i].anchoredPosition;
                _belowElements[i].anchoredPosition = new Vector2(pos.x, _belowBaseY[i] - shift);
            }
        }
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
        PositionSlot(_views[index], index);
        UpdateLayout();
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= _views.Length) return;
        if (_views[index] == null) return;

        SafeDestroy(_views[index].gameObject);
        _views[index] = null;
        UpdateLayout();
    }

    /// <summary>按 行=index/每行数、列=index%每行数 定位槽位（超过每行数自动换行）。
    /// 槽位锚点固定为容器左上角（0,1），anchoredPosition 直接以左上角为原点排列。</summary>
    private void PositionSlot(ItemSlotView view, int index)
    {
        if (view == null || _container == null) return;
        var rt = view.transform as RectTransform;
        if (rt == null) return;

        // 关键：槽位默认 anchor=(0.5,0.5) 会相对容器中心定位（显示在屏幕中央）。
        // 必须把槽位锚点改为容器左上角 (0,1)，与容器的 anchor/pivot 一致，从左上起排。
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        int row = index / Mathf.Max(1, _columns);
        int col = index % Mathf.Max(1, _columns);
        // 左上角为原点：x 向右（列×行宽 + 半格居中），y 向下（行×行高 + 半格居中）
        float x = 4f + col * _rowHeight + 32f;
        float y = -(4f + row * _rowHeight + 32f);
        rt.anchoredPosition = new Vector2(x, y);
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
