using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道具详情面板 — 点击道具槽位时显示完整信息。
/// 位于 ItemPanel 旁或覆盖层。
/// </summary>
public class ItemDetailPanel : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descText;
    [SerializeField] private Image _qualityFrame;
    [SerializeField] private Button _closeButton;

    private void Awake()
    {
        gameObject.SetActive(false);
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
    }

    public void Show(ItemData item)
    {
        if (item == null) return;
        gameObject.SetActive(true);

        if (_icon != null)
        {
            _icon.sprite = item.icon;
            _icon.enabled = item.icon != null;
        }
        if (_nameText != null)
        {
            _nameText.text = item.itemName;
            _nameText.color = ItemPickup.QualityToColor(item.quality);
        }
        if (_descText != null)
            _descText.text = item.description;
        if (_qualityFrame != null)
            _qualityFrame.color = ItemPickup.QualityToColor(item.quality);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
