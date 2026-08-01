using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道具槽位 View — 纯渲染，不引用任何游戏逻辑。
/// 由 ItemPanel 管理，由 ItemUIController 调用 Refresh() 更新。
/// </summary>
public class ItemSlotView : MonoBehaviour
{
    [Header("图标")]
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _emptyIcon;

    [Header("品质框")]
    [SerializeField] private Image _qualityFrame;

    [Header("信息")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descText;
    [SerializeField] private TMP_Text _countText;

    [Header("Tooltip触发器")]
    [SerializeField] private Button _tooltipTrigger;

    /// <summary>当前绑定的道具数据</summary>
    public ItemData BoundItem { get; private set; }

    private void Awake()
    {
        if (_tooltipTrigger != null)
        {
            _tooltipTrigger.onClick.AddListener(() =>
                ItemPanel.Instance?.OnSlotClicked(this));
        }
    }

    public void Refresh(in ItemViewData data, ItemData boundItem)
    {
        BoundItem = boundItem;

        if (_icon != null)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;
        }
        if (_emptyIcon != null)
            _emptyIcon.SetActive(data.Icon == null);

        if (_qualityFrame != null)
        {
            _qualityFrame.color = QualityToColor(data.Quality);
            _qualityFrame.gameObject.SetActive(true);
        }

        if (_nameText != null)
        {
            _nameText.text = data.Name;
            _nameText.color = QualityToColor(data.Quality);
        }

        if (_descText != null)
            _descText.text = data.Description;

        if (_countText != null)
        {
            _countText.text = data.Count > 1 ? $"x{data.Count}" : "";
            _countText.gameObject.SetActive(data.Count > 1);
        }
    }

    public void SetEmpty()
    {
        BoundItem = null;

        if (_icon != null) _icon.enabled = false;
        if (_emptyIcon != null) _emptyIcon.SetActive(true);
        if (_qualityFrame != null) _qualityFrame.gameObject.SetActive(false);
        if (_nameText != null) _nameText.text = "";
        if (_descText != null) _descText.text = "";
        if (_countText != null) _countText.text = "";
    }

    private static Color QualityToColor(ItemQuality quality) => quality switch
    {
        ItemQuality.Common    => new Color(0.78f, 0.78f, 0.78f),
        ItemQuality.Uncommon  => new Color(0.36f, 0.65f, 1f),
        ItemQuality.Rare      => new Color(0.68f, 0.36f, 1f),
        ItemQuality.Legendary => new Color(1f, 0.65f, 0.15f),
        _                     => Color.white
    };
}
