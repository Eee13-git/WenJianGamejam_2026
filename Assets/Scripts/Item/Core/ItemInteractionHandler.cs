using UnityEngine;

/// <summary>
/// 道具交互处理器 — 挂载在 Player 上。
/// 负责:
///   1. 检测附近可拾取的 ItemPickup (Physics2D.OverlapCircleNonAlloc)
///   2. 管理最近的目标 + 高亮状态
///   3. 驱动 ItemDetailPopup 显隐
///   4. 响应 F 键拾取道具
///
/// 性能: 每 0.1 秒扫描一次, 使用 NonAlloc 减少 GC。
/// </summary>
public class ItemInteractionHandler : MonoBehaviour
{
    [Header("交互设置")]
    [SerializeField] private float _detectRadius = 2f;
    [SerializeField] private float _scanInterval = 0.1f;
    [SerializeField] private KeyCode _interactKey = KeyCode.F;

    [Header("UI 引用")]
    [SerializeField] private ItemDetailPopup _detailPopup;

    [Header("引用")]
    [SerializeField] private ItemManager _itemManager;

    private ItemPickup _nearestPickup;
    private ItemPickup _highlightedPickup;

    private readonly Collider2D[] _overlapResults = new Collider2D[32];
    private float _scanTimer;

    private void Awake()
    {
        if (_itemManager == null)
            _itemManager = GetComponent<ItemManager>();
        if (_detailPopup == null)
            _detailPopup = FindObjectOfType<ItemDetailPopup>(true);
    }

    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = _scanInterval;
            ScanForPickups();
        }

        if (Input.GetKeyDown(_interactKey))
            TryPickupNearest();
    }

    private void ScanForPickups()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, _detectRadius, _overlapResults);

        ItemPickup closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var pickup = _overlapResults[i].GetComponent<ItemPickup>();
            if (pickup == null || pickup.itemData == null) continue;

            float dist = Vector2.Distance(transform.position, pickup.transform.position);
            if (dist < closestDist && dist <= pickup.pickupRadius)
            {
                closestDist = dist;
                closest = pickup;
            }
        }

        // 取消旧高亮
        if (_highlightedPickup != null && _highlightedPickup != closest)
        {
            _highlightedPickup.SetInRange(false);
            _highlightedPickup = null;
        }

        // 设置新高亮 + 弹窗
        if (closest != null && closest != _highlightedPickup)
        {
            closest.SetInRange(true);
            _highlightedPickup = closest;

            if (_detailPopup != null)
                _detailPopup.Show(closest);
        }

        // 无目标 → 隐藏弹窗
        if (closest == null && _nearestPickup != null)
        {
            if (_detailPopup != null)
                _detailPopup.Hide();
        }

        _nearestPickup = closest;
    }

    private void TryPickupNearest()
    {
        if (_nearestPickup == null || _itemManager == null) return;

        ItemData data = _nearestPickup.Pickup();

        if (_itemManager.AcquireItem(data))
        {
            if (_detailPopup != null)
                _detailPopup.Hide();

            var toDestroy = _nearestPickup.gameObject;
            _nearestPickup = null;
            _highlightedPickup = null;
            Destroy(toDestroy);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _detectRadius);
    }
#endif
}
