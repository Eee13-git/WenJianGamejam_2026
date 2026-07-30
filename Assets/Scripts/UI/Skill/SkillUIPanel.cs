using UnityEngine;

/// <summary>
/// 技能 UI 面板 —— 管理 SkillSlotView 的创建与刷新。
/// </summary>
public class SkillUIPanel : MonoBehaviour
{
    [Header("预制体与容器")]
    [SerializeField] private SkillSlotView _slotPrefab;
    [SerializeField] private Transform _container;

    private SkillSlotView[] _views = System.Array.Empty<SkillSlotView>();

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

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
