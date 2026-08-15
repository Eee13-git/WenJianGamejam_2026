using UnityEngine;

/// <summary>
/// 道具拾取物 — 场景中可拾取的道具 GameObject。
///
/// 交互方式 (近距离按键):
///   1. 玩家靠近时高亮显示 + 弹出详情 UI
///   2. 按 F 键拾取
///   3. 不再通过碰撞自动拾取
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Header("道具数据")]
    public ItemData itemData;

    [Header("交互距离")]
    [Tooltip("玩家可交互的最大距离")]
    public float pickupRadius = 1.5f;

    [Header("表现")]
    public GameObject pickupEffectPrefab;

    [Header("漂浮动画")]
    [SerializeField] private bool _enableFloatAnimation = true;
    [SerializeField] private float _floatAmplitude = 0.15f;
    [SerializeField] private float _floatFrequency = 2f;
    [SerializeField] private bool _enableRotation = true;
    [SerializeField] private float _rotationSpeed = 30f;

    [Header("高亮")]
    [SerializeField] private float _highlightScale = 1.3f;
    [SerializeField] private Color _highlightTint = new Color(1f, 1f, 0.7f, 1f);

    /// <summary>当前是否在玩家交互范围内</summary>
    public bool IsInRange { get; private set; }

    /// <summary>是否为商店商品（靠近时走购买流程而非拾取）</summary>
    public bool IsShopItem { get; set; }

    /// <summary>商店价格（从 ItemData.price 读取，应用全局折扣）</summary>
    public int ShopPrice
    {
        get
        {
            int basePrice = itemData != null ? itemData.price : 10;
            float discount = ShopManager.GlobalDiscount;
            if (discount > 0f)
                return Mathf.Max(1, Mathf.RoundToInt(basePrice * (1f - discount)));
            return basePrice;
        }
    }

    private Vector3 _startPosition;
    private SpriteRenderer _spriteRenderer;
    private Color _baseColor;
    private Vector3 _baseScale;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _startPosition = transform.position;
        _baseScale = transform.localScale;

        if (_spriteRenderer != null)
        {
            _baseColor = _spriteRenderer.color;

            if (itemData != null && itemData.icon != null)
                _spriteRenderer.sprite = itemData.icon;
        }
    }

    private void Update()
    {
        float t = Time.time * _floatFrequency;
        Vector3 floatOffset = _enableFloatAnimation
            ? Vector3.up * (Mathf.Sin(t) * _floatAmplitude)
            : Vector3.zero;

        // 高亮（始终运行）
        if (IsInRange)
        {
            transform.localScale = _baseScale * _highlightScale;
            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.Lerp(_baseColor, _highlightTint, 0.5f);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, Time.deltaTime * 8f);
            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.Lerp(_spriteRenderer.color, _baseColor, Time.deltaTime * 8f);
        }

        // 漂浮位移（仅 _enableFloatAnimation 时生效）
        transform.position = _startPosition + floatOffset;

        // 旋转（仅 _enableRotation 时生效）
        if (_enableRotation)
            transform.Rotate(Vector3.forward, _rotationSpeed * Time.deltaTime);
    }

    /// <summary>由 ItemInteractionHandler 调用，标记在范围内</summary>
    public void SetInRange(bool inRange)
    {
        IsInRange = inRange;
    }

    /// <summary>执行拾取 — 返回道具数据</summary>
    public ItemData Pickup()
    {
        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

        return itemData;
    }

    /// <summary>品质 → 颜色映射</summary>
    public static Color QualityToColor(ItemQuality quality) => quality switch
    {
        ItemQuality.Common    => new Color(0.78f, 0.78f, 0.78f),
        ItemQuality.Uncommon  => new Color(0.36f, 0.65f, 1f),
        ItemQuality.Rare      => new Color(0.68f, 0.36f, 1f),
        ItemQuality.Legendary => new Color(1f, 0.65f, 0.15f),
        _                     => Color.white
    };
}
