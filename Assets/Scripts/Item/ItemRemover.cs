using UnityEngine;

/// <summary>
/// 道具移除节点 — 玩家靠近显示提示，按 F 打开道具选择面板。
/// 玩家可选择移除一个道具（触发 OnRemove 还原效果），可移除 n 次。
/// </summary>
public class ItemRemover : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("可移除道具的次数（-1=无限）")]
    [SerializeField] private int _removeCount = 1;

    [Header("交互")]
    [Tooltip("交互范围")]
    [SerializeField] private float _interactRadius = 1.5f;
    [Tooltip("交互按键")]
    [SerializeField] private KeyCode _interactKey = KeyCode.F;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer _sr;
    [SerializeField] private Color _normalColor = new Color(0.8f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color _highlightColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color _depletedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    private bool _playerInRange;
    private bool _depleted;
    private ItemRemoverPanel _panel;
    private ItemRemoverPrompt _prompt;

    private void Awake()
    {
        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    private void Start()
    {
        _panel = FindObjectOfType<ItemRemoverPanel>(true);
        _prompt = FindObjectOfType<ItemRemoverPrompt>(true);
    }

    private void Update()
    {
        if (_depleted) return;

        var player = PlayerManager.Instance?.CurrentPlayer;
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        bool inRange = dist <= _interactRadius;

        if (inRange != _playerInRange)
        {
            _playerInRange = inRange;
            UpdateVisual();
            if (_prompt != null)
            {
                if (inRange) _prompt.Show(transform);
                else _prompt.Hide();
            }
        }

        if (inRange && Input.GetKeyDown(_interactKey) && _panel != null && !_panel.IsVisible)
        {
            var itemManager = player.GetComponent<ItemManager>();
            if (itemManager != null && itemManager.Count > 0)
            {
                if (_prompt != null) _prompt.Hide();
                _panel.Show(itemManager, _removeCount, OnPanelClosed);
            }
        }
    }

    private void OnPanelClosed()
    {
        // 无限次数模式不追踪剩余
        if (_removeCount < 0) return;

        // 面板关闭后检查剩余次数
        if (_panel != null)
        {
            var field = typeof(ItemRemoverPanel).GetField("_remainingCount",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                int remaining = (int)field.GetValue(_panel);
                _removeCount = remaining;
                if (_removeCount <= 0)
                {
                    _depleted = true;
                    UpdateVisual();
                    if (_prompt != null) _prompt.Hide();
                }
            }
        }
    }

    private void UpdateVisual()
    {
        if (_sr == null) return;

        if (_depleted)
            _sr.color = _depletedColor;
        else if (_playerInRange)
            _sr.color = _highlightColor;
        else
            _sr.color = _normalColor;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _interactRadius);
    }
#endif
}
