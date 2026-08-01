using UnityEngine;

/// <summary>
/// 道具 UI Controller — 监听 ItemManager(Model), 通知 ItemPanel(View)。
/// 本类是 Model ↔ View 的唯一耦合点, 沿用 SkillUIController 的模式。
/// </summary>
public class ItemUIController : MonoBehaviour
{
    [Header("Model 引用")]
    [SerializeField] private ItemManager _itemManager;

    [Header("View 引用")]
    [SerializeField] private ItemPanel _panel;

    [Header("初始容量")]
    [SerializeField] private int _initialSlotCount = 16;

    // ==================== 生命周期 ====================

    private void Start()
    {
        if (_itemManager == null)
            _itemManager = ResolveItemManager();

        if (_itemManager == null)
        {
            Debug.LogError("ItemUIController: ItemManager 未赋值", this);
            return;
        }
        if (_panel == null)
        {
            Debug.LogError("ItemUIController: ItemPanel 未赋值", this);
            return;
        }

        _panel.Initialize(_initialSlotCount);

        _itemManager.OnItemAcquired += HandleItemAcquired;
        _itemManager.OnItemRemoved += HandleItemRemoved;

        SyncAll();
    }

    private void OnDestroy()
    {
        if (_itemManager != null)
        {
            _itemManager.OnItemAcquired -= HandleItemAcquired;
            _itemManager.OnItemRemoved -= HandleItemRemoved;
        }
    }

    // ==================== Model 事件回调 ====================

    private void HandleItemAcquired(ItemData item)
    {
        // 找到该道具在列表中的索引
        var items = _itemManager.Items;
        int idx = -1;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == item) { idx = i; break; }
        }
        if (idx >= 0) SyncSlot(idx, item);
    }

    private void HandleItemRemoved(ItemData item)
    {
        SyncAll();
    }

    // ==================== 核心同步 ====================

    private void SyncAll()
    {
        var items = _itemManager.Items;
        for (int i = 0; i < items.Count; i++)
            SyncSlot(i, items[i]);

        // 隐藏空槽
        for (int i = items.Count; i < _initialSlotCount; i++)
            _panel.ClearSlot(i);
    }

    private void SyncSlot(int index, ItemData item)
    {
        int count = _itemManager.GetItemCount(item.itemId);
        ItemViewData data = new ItemViewData
        {
            Icon = item.icon,
            Name = item.itemName,
            Description = item.description,
            Quality = item.quality,
            Count = count
        };
        _panel.RefreshSlot(index, data, item);
    }

    // ==================== 查找 ====================

    private static ItemManager ResolveItemManager()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
            return PlayerManager.Instance.CurrentPlayer.GetComponent<ItemManager>();

        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<ItemManager>() : null;
    }
}
