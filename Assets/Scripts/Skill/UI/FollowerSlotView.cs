using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 同化槽位弹窗行项 —— 显示一个随从槽位：空槽 / 占用（头像 + 名称 Lv + 升级/替换徽标）。
/// 结构由 Editor 构建脚本生成，运行时只填充数据。
/// </summary>
public class FollowerSlotView : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private Image _avatar;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _badgeText;

    /// <summary>槽位索引（0..MaxFollowerCount-1）</summary>
    public int SlotIndex { get; private set; }

    /// <summary>槽位当前随从（空槽为 null）</summary>
    public EnemyFollower BoundFollower { get; private set; }

    /// <summary>该槽位点击后的操作：Placed / Upgraded / Replaced</summary>
    public FollowerSlotResult ActionHint { get; private set; }

    /// <summary>随从头像占位圆（无 sprite 时显示）</summary>
    private Sprite _fallbackIcon;

    private void Awake()
    {
#if UNITY_EDITOR
        _fallbackIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/WhiteCircle.asset");
#endif
    }

    /// <summary>填充槽位数据</summary>
    /// <param name="slotIndex">槽位索引</param>
    /// <param name="follower">槽位当前随从（空槽为 null）</param>
    /// <param name="targetEnemy">被侵蚀的敌人（用于同种判断）</param>
    public void Refresh(int slotIndex, EnemyFollower follower, EnemyCore targetEnemy)
    {
        SlotIndex = slotIndex;
        BoundFollower = follower;

        // 空槽
        if (follower == null)
        {
            ActionHint = FollowerSlotResult.Placed;
            if (_avatar != null)
                _avatar.enabled = false;
            if (_nameText != null)
            {
                _nameText.text = "＋ 空槽";
                _nameText.color = new Color(0.55f, 0.55f, 0.6f, 1f);
            }
            if (_badgeText != null)
            {
                _badgeText.text = "放置";
                _badgeText.color = new Color(0.55f, 0.8f, 1f, 1f);
            }
            return;
        }

        // 占用槽
        var core = follower.Core;
        bool sameType = EnemyFollower.SameType(core, targetEnemy);
        bool dead = core != null && core.Health != null && core.Health.IsDead;

        ActionHint = sameType ? FollowerSlotResult.Upgraded : FollowerSlotResult.Replaced;

        // 头像
        if (_avatar != null)
        {
            Sprite sprite = null;
            if (core != null)
            {
                var sr = core.GetComponent<SpriteRenderer>();
                if (sr != null) sprite = sr.sprite;
            }
            _avatar.sprite = sprite != null ? sprite : _fallbackIcon;
            _avatar.enabled = true;
            // 无 sprite 时显示灰色占位圆
            _avatar.color = sprite != null
                ? (dead ? new Color(1f, 1f, 1f, 0.35f) : Color.white)
                : new Color(0.6f, 0.6f, 0.65f, 0.7f);
        }

        // 名称 + 等级
        if (_nameText != null)
        {
            string name = "随从";
            if (core != null)
            {
                if (core.config != null && !string.IsNullOrEmpty(core.config.displayName))
                    name = core.config.displayName;
                else
                    name = core.gameObject.name;
            }
            string suffix = dead ? "（复活中）" : "";
            _nameText.text = $"{name} Lv.{follower.Level}{suffix}";
            _nameText.color = dead ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
        }

        // 徽标
        if (_badgeText != null)
        {
            if (sameType)
            {
                _badgeText.text = "升级";
                _badgeText.color = new Color(1f, 0.8f, 0.25f, 1f); // 金色
            }
            else
            {
                _badgeText.text = "替换";
                _badgeText.color = new Color(1f, 0.45f, 0.35f, 1f); // 红色
            }
        }
    }
}
