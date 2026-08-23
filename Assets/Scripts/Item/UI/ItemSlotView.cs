using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 道具槽位 View — 纯渲染，鼠标悬停时弹出详情 tooltip。
/// 由 ItemPanel 管理，由 ItemUIController 调用 Refresh() 更新。
/// </summary>
public class ItemSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("图标")]
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _emptyIcon;

    [Header("品质框")]
    [SerializeField] private Image _qualityFrame;

    [Header("信息")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _countText;

    /// <summary>当前绑定的道具数据</summary>
    public ItemData BoundItem { get; private set; }

    private Image _background;

    /// <summary>外部 hover 处理器，非空时替代默认 ItemPanel.Instance 行为</summary>
    private System.Action<ItemSlotView> _hoverEnterHandler;
    private System.Action _hoverExitHandler;

    private void Awake()
    {
        _background = GetComponent<Image>();
    }

    /// <summary>设置自定义 hover 回调（用于 ItemRemoverPanel 等非 ItemPanel 场景）</summary>
    public void SetHoverHandler(System.Action<ItemSlotView> enterHandler, System.Action exitHandler)
    {
        _hoverEnterHandler = enterHandler;
        _hoverExitHandler = exitHandler;
    }

    public void Refresh(in ItemViewData data, ItemData boundItem)
    {
        BoundItem = boundItem;

        // 有道具时显示极微弱的暗底
        if (_background != null)
            _background.color = new Color(0.1f, 0.1f, 0.12f, 0.3f);

        if (_icon != null)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;

            // 移除旧动画组件
            var existingAnim = _icon.GetComponent<ItemIconAnimator>();
            if (existingAnim != null) Destroy(existingAnim);
        }
        if (_emptyIcon != null)
            _emptyIcon.SetActive(data.Icon == null);

        // 如果道具有动画帧，添加动画组件
        if (boundItem != null && boundItem.animationFrames != null
            && boundItem.animationFrames.Length > 1 && _icon != null)
        {
            var anim = _icon.gameObject.AddComponent<ItemIconAnimator>();
            anim.Initialize(boundItem.animationFrames, boundItem.animationFPS, true);
        }

        if (_qualityFrame != null)
        {
            // 品质框只显示为颜色边框，Image 设为透明底色 + 薄边框效果
            _qualityFrame.color = new Color(
                QualityToColor(data.Quality).r,
                QualityToColor(data.Quality).g,
                QualityToColor(data.Quality).b,
                0.35f);
            _qualityFrame.gameObject.SetActive(true);
        }

        if (_nameText != null)
        {
            _nameText.text = data.Name;
            _nameText.color = QualityToColor(data.Quality);
        }

        if (_countText != null)
        {
            _countText.text = data.Count > 1 ? $"x{data.Count}" : "";
            _countText.gameObject.SetActive(data.Count > 1);
        }
    }

    public void SetEmpty()
    {
        BoundItem = null;

        if (_background != null)
            _background.color = new Color(0f, 0f, 0f, 0f); // 完全透明

        if (_icon != null)
        {
            _icon.enabled = false;
            // 移除动画组件
            var existingAnim = _icon.GetComponent<ItemIconAnimator>();
            if (existingAnim != null) Destroy(existingAnim);
        }
        if (_emptyIcon != null) _emptyIcon.SetActive(false);
        if (_qualityFrame != null) _qualityFrame.gameObject.SetActive(false);
        if (_nameText != null) _nameText.text = "";
        if (_countText != null) _countText.text = "";
    }

    // ==================== 鼠标悬停 ====================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_hoverEnterHandler != null)
            _hoverEnterHandler(this);
        else
            ItemPanel.Instance?.ShowHoverTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_hoverExitHandler != null)
            _hoverExitHandler();
        else
            ItemPanel.Instance?.HideHoverTooltip();
    }

    // ==================== 工具 ====================

    private static Color QualityToColor(ItemQuality quality) => quality switch
    {
        ItemQuality.Common    => new Color(0.78f, 0.78f, 0.78f),
        ItemQuality.Uncommon  => new Color(0.36f, 0.65f, 1f),
        ItemQuality.Rare      => new Color(0.68f, 0.36f, 1f),
        ItemQuality.Legendary => new Color(1f, 0.65f, 0.15f),
        _                     => Color.white
    };
}
