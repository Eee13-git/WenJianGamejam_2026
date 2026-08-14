using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道具移除节点的 Screen Space Overlay 浮动提示 — 与商店弹窗同款。
/// 跟随世界坐标，CanvasGroup 淡入淡出，深色半透明背景。
/// 挂在 Canvas 下的空 GameObject 上。
/// </summary>
public class ItemRemoverPrompt : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _promptText;

    [Header("样式")]
    [SerializeField] private float _fadeSpeed = 10f;
    [SerializeField] private Vector2 _screenOffset = new Vector2(0, 60f);

    private RectTransform _rect;
    private Camera _mainCam;
    private Transform _target;
    private bool _visible;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _mainCam = Camera.main;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        // 注意：不要在 Awake 里 SetActive(false)。
        // 该对象在场景中初始为 inactive，首次 Show() 的 SetActive(true)
        // 才会触发 Awake，若此处再 SetActive(false) 会导致首次提示不显示。
    }

    public void Show(Transform target)
    {
        if (target == null) return;

        _target = target;
        gameObject.SetActive(true);
        _visible = true;

        if (_promptText != null)
            _promptText.text = "点击F键移除道具";
        if (_background != null)
            _background.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);
    }

    public void Hide()
    {
        _target = null;
        _visible = false;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_target == null || !_visible)
        {
            if (_canvasGroup != null && _canvasGroup.alpha > 0f)
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, _fadeSpeed * Time.deltaTime);
            return;
        }

        if (_mainCam != null)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(_target.position);
            _rect.position = screenPos + (Vector3)_screenOffset;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, _fadeSpeed * Time.deltaTime);
    }
}
