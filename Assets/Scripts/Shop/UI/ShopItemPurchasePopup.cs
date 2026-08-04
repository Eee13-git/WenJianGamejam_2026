using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 商店购买弹窗 — 玩家靠近标记为 IsShopItem 的 ItemPickup 时悬浮显示。
/// 显示道具信息 + 价格 + 购买按钮。
/// Screen Space Overlay，跟随拾取物世界坐标。
/// </summary>
public class ShopItemPurchasePopup : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private TMP_Text _promptText;
    [SerializeField] private Button _buyButton;

    [Header("样式")]
    [SerializeField] private float _fadeSpeed = 10f;
    [SerializeField] private Vector2 _screenOffset = new Vector2(0, 100f);

    [Header("购买按键")]
    [SerializeField] private KeyCode _buyKey = KeyCode.F;

    /// <summary>当前正在展示的拾取物</summary>
    public ItemPickup CurrentTarget { get; private set; }

    public bool IsVisible { get; private set; }

    private RectTransform _rect;
    private Camera _mainCam;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _mainCam = Camera.main;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;

        if (_buyButton != null)
            _buyButton.onClick.AddListener(OnBuyClicked);

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (CurrentTarget == null || !IsVisible) return;

        // F 键购买
        if (Input.GetKeyDown(_buyKey))
        {
            // 购买由 ItemInteractionHandler 统一处理 F 键
            // 这里只做按钮点击的备选
            OnBuyClicked();
        }
    }

    private void LateUpdate()
    {
        if (CurrentTarget == null || !IsVisible)
        {
            if (_canvasGroup != null && _canvasGroup.alpha > 0f)
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, _fadeSpeed * Time.deltaTime);
            return;
        }

        // 跟随世界坐标
        if (_mainCam != null)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(CurrentTarget.transform.position);
            _rect.position = screenPos + (Vector3)_screenOffset;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, _fadeSpeed * Time.deltaTime);
    }

    /// <summary>显示弹窗</summary>
    public void Show(ItemPickup pickup)
    {
        if (pickup == null || pickup.itemData == null) return;

        // 如果正在显示另一个，先隐藏
        if (CurrentTarget != null && CurrentTarget != pickup)
            Hide();

        CurrentTarget = pickup;
        gameObject.SetActive(true);
        IsVisible = true;

        var data = pickup.itemData;

        if (_icon != null)
        {
            _icon.sprite = data.icon;
            _icon.enabled = data.icon != null;
        }
        if (_nameText != null)
        {
            _nameText.text = data.itemName;
            _nameText.color = ItemPickup.QualityToColor(data.quality);
        }
        if (_descText != null)
            _descText.text = data.description;

        RefreshPrice(pickup);

        if (_background != null)
            _background.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);
    }

    /// <summary>隐藏弹窗</summary>
    public void Hide()
    {
        CurrentTarget = null;
        IsVisible = false;
        gameObject.SetActive(false);
    }

    /// <summary>刷新价格显示（ATP 变化时由 ItemInteractionHandler 调用）</summary>
    public void RefreshPrice(ItemPickup pickup)
    {
        if (pickup == null) return;

        int price = pickup.ShopPrice;
        var cm = ResolveCurrencyManager();

        bool canAfford = cm != null && cm.ATP >= price;
        int currentATP = cm != null ? cm.ATP : 0;

        if (_priceText != null)
        {
            _priceText.text = canAfford
                ? $"<color=#D9AA4D>{price} ATP</color>"
                : $"<color=#FF4444>{price} ATP</color>  (当前: {currentATP})";
        }

        if (_promptText != null)
        {
            _promptText.text = canAfford
                ? $"[F] 购买"
                : $"<color=#FF4444>ATP 不足</color>";
        }

        if (_buyButton != null)
            _buyButton.interactable = canAfford;
    }

    private void OnBuyClicked()
    {
        if (CurrentTarget == null) return;

        // 直接执行购买逻辑
        var player = PlayerManager.Instance?.CurrentPlayer;
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var cm = player.GetComponent<CurrencyManager>();
        var im = player.GetComponent<ItemManager>();
        if (cm == null || im == null) return;

        int price = CurrentTarget.ShopPrice;
        if (!cm.Spend(price))
        {
            RefreshPrice(CurrentTarget);
            return;
        }

        im.AcquireItem(CurrentTarget.itemData);

        var pickup = CurrentTarget;
        Hide();
        Destroy(pickup.gameObject);
    }

    private CurrencyManager ResolveCurrencyManager()
    {
        var player = PlayerManager.Instance?.CurrentPlayer;
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<CurrencyManager>() : null;
    }
}
