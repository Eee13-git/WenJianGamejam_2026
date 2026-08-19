using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 装配槽弹窗行项 —— 显示一个玩家技能槽：键位 + 当前技能（图标/名/Lv）+ 徽标（装备/升级/替换/已满）。
/// 结构由 Editor 构建脚本生成，运行时只填充数据。
/// </summary>
public class SkillEquipSlotView : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private TMP_Text _keyText;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _badgeText;

    /// <summary>槽位索引</summary>
    public int SlotIndex { get; private set; }

    /// <summary>槽位当前技能（空槽为 null）</summary>
    public SkillInstance CurrentSkill { get; private set; }

    /// <summary>点击后行为：装备 / 升级 / 替换 / 已满（已满置灰不可点）</summary>
    public string ActionBadge { get; private set; }

    /// <summary>技能图标占位圆（无图标技能显示灰色圆点）</summary>
    private Sprite _fallbackIcon;

    private void Awake()
    {
#if UNITY_EDITOR
        _fallbackIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/WhiteCircle.asset");
#endif
    }

    /// <summary>填充槽位数据</summary>
    /// <param name="slotIndex">槽位索引</param>
    /// <param name="keyLabel">键位名（Q/E/Z/X）</param>
    /// <param name="current">槽位当前技能（空槽为 null）</param>
    /// <param name="newSkillId">玩家选定要夺取的技能 ID</param>
    public void Refresh(int slotIndex, string keyLabel, SkillInstance current, string newSkillId)
    {
        SlotIndex = slotIndex;
        CurrentSkill = current;

        if (_keyText != null)
            _keyText.text = keyLabel;

        // 图标 + 名称
        if (current == null)
        {
            if (_icon != null)
                _icon.enabled = false;
            if (_nameText != null)
            {
                _nameText.text = "空槽";
                _nameText.color = new Color(0.55f, 0.55f, 0.6f, 1f);
            }
        }
        else
        {
            if (_icon != null)
            {
                bool hasIcon = current.Data.icon != null;
                _icon.sprite = hasIcon ? current.Data.icon : _fallbackIcon;
                _icon.enabled = true;
                // 无图标时显示灰色占位圆
                _icon.color = hasIcon
                    ? Color.white
                    : new Color(0.6f, 0.6f, 0.65f, 0.7f);
            }
            if (_nameText != null)
            {
                _nameText.text = $"{current.Data.skillName} Lv.{current.Level}";
                _nameText.color = Color.white;
            }
        }

        // 徽标
        if (_badgeText != null)
        {
            if (current == null)
            {
                ActionBadge = "装备";
                _badgeText.text = "装备";
                _badgeText.color = new Color(0.55f, 0.8f, 1f, 1f);
            }
            else if (current.Data.skillId == newSkillId)
            {
                if (current.IsMaxLevel)
                {
                    ActionBadge = "已满";
                    _badgeText.text = "已满";
                    _badgeText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                }
                else
                {
                    ActionBadge = "升级";
                    _badgeText.text = "升级";
                    _badgeText.color = new Color(1f, 0.8f, 0.25f, 1f);
                }
            }
            else
            {
                ActionBadge = "替换";
                _badgeText.text = "替换";
                _badgeText.color = new Color(1f, 0.45f, 0.35f, 1f);
            }
        }
    }
}
