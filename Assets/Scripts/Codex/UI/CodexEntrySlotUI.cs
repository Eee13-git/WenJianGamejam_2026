using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 图鉴条目列表单元 — 显示图标、名称、锁定状态。
/// 挂在 CodexEntrySlot.prefab 上。
/// </summary>
public class CodexEntrySlotUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private GameObject _lockOverlay;

    public void Setup(Sprite icon, string displayName, bool unlocked)
    {
        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.gameObject.SetActive(icon != null && unlocked);
        }
        if (_nameText != null)
            _nameText.text = displayName;
        if (_lockOverlay != null)
            _lockOverlay.SetActive(!unlocked);
    }
}
