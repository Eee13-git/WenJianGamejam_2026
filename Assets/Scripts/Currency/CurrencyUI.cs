using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 货币 UI — 显示 ATP 数量。
/// 挂在 SkillUICanvas/CurrencyUI 上，监听 CurrencyManager.OnATPChanged。
/// </summary>
public class CurrencyUI : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private Sprite _atpIcon;

    private CurrencyManager _manager;

    private void Start()
    {
        // 查找 Player 上的 CurrencyManager
        var player = FindObjectOfType<PlayerManager>(true);
        if (player != null && player.CurrentPlayer != null)
            _manager = player.CurrentPlayer.GetComponent<CurrencyManager>();

        if (_manager == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _manager = p.GetComponent<CurrencyManager>();
        }

        if (_manager == null)
        {
            Debug.LogWarning("CurrencyUI: 找不到 CurrencyManager");
            return;
        }

        // 加载图标
        if (_atpIcon != null && _icon != null)
            _icon.sprite = _atpIcon;

        // 初始同步
        Refresh(_manager.ATP);

        // 监听变化
        _manager.OnATPChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (_manager != null)
            _manager.OnATPChanged -= Refresh;
    }

    private void Refresh(int amount)
    {
        if (_amountText != null)
            _amountText.text = amount.ToString();
    }
}
