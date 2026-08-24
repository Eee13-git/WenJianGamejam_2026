using UnityEngine;
using TMPro;

/// <summary>
/// 技能 UI 面板 —— 管理 SkillSlotView 的创建与刷新。鼠标悬停时显示技能详情 tooltip。
/// </summary>
public class SkillUIPanel : MonoBehaviour
{
    [Header("预制体与容器")]
    [SerializeField] private SkillSlotView _slotPrefab;
    [SerializeField] private Transform _container;

    [Header("悬停提示")]
    [SerializeField] private GameObject _hoverTooltip;
    [SerializeField] private TMP_Text _hoverName;
    [SerializeField] private TMP_Text _hoverDesc;

    public static SkillUIPanel Instance { get; private set; }

    private SkillSlotView[] _views = System.Array.Empty<SkillSlotView>();

    private void Awake()
    {
        Instance = this;
        if (_hoverTooltip != null) _hoverTooltip.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Controller 调用，按数量创建槽位 View</summary>
    public void Initialize(int slotCount)
    {
        // 清理旧子对象
        for (int i = _container.childCount - 1; i >= 0; i--)
            SafeDestroy(_container.GetChild(i).gameObject);

        _views = new SkillSlotView[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            SkillSlotView view = Instantiate(_slotPrefab, _container);
            view.name = $"SkillSlot_{i}";
            _views[i] = view;
        }
    }

    /// <summary>
    /// 确保槽位 View 数量不少于 slotCount，不足时增量创建新视图。
    /// 用于运行时技能槽扩展（PlayerSkillManager.ExpandSlots）后 UI 自动补齐。
    /// </summary>
    public void EnsureSlotCount(int slotCount)
    {
        if (slotCount <= _views.Length) return;
        int oldCount = _views.Length;

        var newViews = new SkillSlotView[slotCount];
        System.Array.Copy(_views, newViews, oldCount);
        for (int i = oldCount; i < slotCount; i++)
        {
            SkillSlotView view = Instantiate(_slotPrefab, _container);
            view.name = $"SkillSlot_{i}";
            newViews[i] = view;
        }
        _views = newViews;
    }

    /// <summary>Controller 调用，刷新指定槽位</summary>
    public void RefreshSlot(int index, in SkillViewData data)
    {
        if (index < 0 || index >= _views.Length) return;
        _views[index]?.Refresh(data);
    }

    /// <summary>获取指定槽位 View（供 Controller 绑定按钮事件）</summary>
    public SkillSlotView GetSlotView(int index)
    {
        if (index < 0 || index >= _views.Length) return null;
        return _views[index];
    }

    // ==================== 悬停 tooltip ====================

    /// <summary>鼠标悬停到技能槽时显示技能详情</summary>
    public void ShowHoverTooltip(SkillSlotView slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.SkillName)) return;

        if (_hoverName != null)
            _hoverName.text = slot.SkillName;
        if (_hoverDesc != null)
            _hoverDesc.text = slot.SkillDescription;

        if (_hoverTooltip != null)
        {
            _hoverTooltip.SetActive(true);
            var rt = _hoverTooltip.GetComponent<RectTransform>();
            if (rt != null)
                rt.position = slot.transform.position + new Vector3(40f, 40f, 0f);
        }
    }

    /// <summary>鼠标离开时隐藏技能详情</summary>
    public void HideHoverTooltip()
    {
        if (_hoverTooltip != null)
            _hoverTooltip.SetActive(false);
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
