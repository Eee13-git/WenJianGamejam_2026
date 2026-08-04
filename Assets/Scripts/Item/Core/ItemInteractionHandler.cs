using UnityEngine;

/// <summary>
/// 道具交互处理器 — 挂载在 Player 上。
/// 负责:
///   1. 检测附近可拾取的 ItemPickup / 可购买的 ShopItemPickup (Physics2D.OverlapCircleNonAlloc)
///   2. 管理最近的目标 + 高亮状态
///   3. 驱动 ItemDetailPopup（拾取）或 ShopItemPurchasePopup（购买）显隐
///   4. 响应 F 键拾取/购买
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
    [SerializeField] private ShopItemPurchasePopup _shopPurchasePopup;

    [Header("引用")]
    [SerializeField] private ItemManager _itemManager;
    [SerializeField] private CurrencyManager _currencyManager;

    private ItemPickup _nearestPickup;
    private ItemPickup _highlightedPickup;

    private readonly Collider2D[] _overlapResults = new Collider2D[32];
    private float _scanTimer;

    private void Awake()
    {
        if (_itemManager == null)
            _itemManager = GetComponent<ItemManager>();
        if (_currencyManager == null)
            _currencyManager = GetComponent<CurrencyManager>();
        if (_detailPopup == null)
            _detailPopup = FindObjectOfType<ItemDetailPopup>(true);
        if (_shopPurchasePopup == null)
            _shopPurchasePopup = FindObjectOfType<ShopItemPurchasePopup>(true);
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
            TryInteractNearest();
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

            // 商店物品 → 购买弹窗；普通道具 → 拾取弹窗
            if (closest.IsShopItem)
            {
                if (_detailPopup != null && _detailPopup.IsVisible) _detailPopup.Hide();
                if (_shopPurchasePopup != null) _shopPurchasePopup.Show(closest);
            }
            else
            {
                if (_shopPurchasePopup != null && _shopPurchasePopup.IsVisible) _shopPurchasePopup.Hide();
                if (_detailPopup != null) _detailPopup.Show(closest);
            }
        }

        // 无目标 → 隐藏所有弹窗
        if (closest == null && _nearestPickup != null)
        {
            if (_detailPopup != null) _detailPopup.Hide();
            if (_shopPurchasePopup != null) _shopPurchasePopup.Hide();
        }

        _nearestPickup = closest;
    }

    private void TryInteractNearest()
    {
        if (_nearestPickup == null) return;

        if (_nearestPickup.IsShopItem)
        {
            // 购买流程
            TryBuyPickup(_nearestPickup);
        }
        else
        {
            // 拾取流程
            TryPickupNearest();
        }
    }

    private void TryBuyPickup(ItemPickup pickup)
    {
        if (pickup == null || pickup.itemData == null) return;
        if (_currencyManager == null)
        {
            _currencyManager = GetComponent<CurrencyManager>();
            if (_currencyManager == null) return;
        }
        if (_itemManager == null)
        {
            _itemManager = GetComponent<ItemManager>();
            if (_itemManager == null) return;
        }

        int price = pickup.ShopPrice;

        if (!_currencyManager.Spend(price))
        {
            if (_shopPurchasePopup != null) _shopPurchasePopup.RefreshPrice(pickup);
            return;
        }

        _itemManager.AcquireItem(pickup.itemData);
        pickup.Pickup(); // 播放特效

        if (_shopPurchasePopup != null) _shopPurchasePopup.Hide();

        _nearestPickup = null;
        _highlightedPickup = null;
        Destroy(pickup.gameObject);
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
