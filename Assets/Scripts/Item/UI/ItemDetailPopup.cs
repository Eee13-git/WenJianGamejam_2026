using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道具详情弹窗 — 玩家靠近道具时悬浮显示。
/// Screen Space Overlay，跟随道具世界坐标。
/// 挂在 Canvas 下的空 GameObject 上。
/// </summary>
public class ItemDetailPopup : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descText;
    [SerializeField] private TMP_Text _promptText;

    [Header("样式")]
    [SerializeField] private float _fadeSpeed = 10f;
    [SerializeField] private Vector2 _screenOffset = new Vector2(0, 80f);

    public bool IsVisible { get; private set; }

    private RectTransform _rect;
    private ItemPickup _currentTarget;
    private Camera _mainCam;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _mainCam = Camera.main;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void Show(ItemPickup pickup)
    {
        if (pickup == null || pickup.itemData == null) return;

        _currentTarget = pickup;
        gameObject.SetActive(true);

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
        if (_promptText != null)
            _promptText.text = "[F] 拾取";

        if (_background != null)
            _background.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

        IsVisible = true;
    }

    public void Hide()
    {
        _currentTarget = null;
        IsVisible = false;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_currentTarget == null || !IsVisible)
        {
            if (_canvasGroup != null && _canvasGroup.alpha > 0f)
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, _fadeSpeed * Time.deltaTime);
            return;
        }

        if (_mainCam != null)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(_currentTarget.transform.position);
            _rect.position = screenPos + (Vector3)_screenOffset;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, _fadeSpeed * Time.deltaTime);
    }
}
