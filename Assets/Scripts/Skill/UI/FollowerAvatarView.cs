using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 随从列表栏行项 —— 左侧随从头像 + 名称/Lv，右侧随从技能冷却图标组。
/// 悬停头像时由 FollowerPanel 弹出详细属性 Tooltip。
/// 结构由 Editor 构建脚本生成（FollowerAvatarView.prefab），运行时只填充数据。
/// </summary>
public class FollowerAvatarView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 元素")]
    [SerializeField] private Image _avatar;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Transform _skillRoot;

    [Header("技能图标模板（子对象：Icon / Overlay / CdText）")]
    [SerializeField] private GameObject _skillItemTemplate;

    /// <summary>当前绑定的随从</summary>
    public EnemyFollower BoundFollower { get; private set; }

    // 技能图标运行时引用
    private class SkillItemRef
    {
        public GameObject Root;
        public Image Icon;
        public Image Overlay;
        public TMP_Text CdText;
        public SkillInstance Skill;
    }

    private readonly List<SkillItemRef> _skillItems = new();

    /// <summary>技能图标占位圆（无图标技能显示灰色圆点，保持面板信息完整）</summary>
    private Sprite _fallbackIcon;

    private void Awake()
    {
#if UNITY_EDITOR
        _fallbackIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/WhiteCircle.asset");
#endif
    }

    /// <summary>填充行数据（仅当随从列表修订变化时调用）</summary>
    public void Refresh(EnemyFollower follower)
    {
        BoundFollower = follower;

        var core = follower != null ? follower.Core : null;
        var stats = core != null ? core.Health : null;

        // 头像：取 SpriteRenderer 当前帧 Sprite，null 时用占位圆
        if (_avatar != null)
        {
            Sprite sprite = null;
            if (core != null)
            {
                var sr = core.GetComponent<SpriteRenderer>();
                if (sr != null) sprite = sr.sprite;
            }
            _avatar.sprite = sprite;
            _avatar.enabled = sprite != null;

            // 复活中置灰
            bool dead = stats != null && stats.IsDead;
            _avatar.color = dead ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        // 名称 + 等级
        if (_nameText != null)
        {
            string displayName = GetDisplayName(core);
            bool dead = stats != null && stats.IsDead;
            string suffix = dead ? "（复活中）" : "";
            int lv = follower != null ? follower.Level : 1;
            _nameText.text = $"{displayName} Lv.{lv}{suffix}";
        }

        RebuildSkillItems(follower);
    }

    /// <summary>每帧刷新技能冷却（覆盖层填充量 + 秒数文本）</summary>
    public void UpdateCooldowns()
    {
        foreach (var item in _skillItems)
        {
            if (item == null || item.Skill == null) continue;

            bool cooling = item.Skill.IsCoolingDown && item.Skill.CooldownRemaining > 0f;

            if (item.Overlay != null)
            {
                item.Overlay.fillAmount = item.Skill.CooldownPercent;
                item.Overlay.enabled = cooling;
            }
            if (item.CdText != null)
            {
                item.CdText.gameObject.SetActive(cooling);
                if (cooling)
                    item.CdText.text = item.Skill.CooldownRemaining.ToString("F1");
            }
        }
    }

    // ── 悬停 ──

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (BoundFollower == null) return;
        FollowerPanel.Instance?.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        FollowerPanel.Instance?.HideTooltip();
    }

    private void OnDisable()
    {
        // 行被销毁/隐藏时收起 Tooltip，避免残留
        if (BoundFollower != null)
            FollowerPanel.Instance?.HideTooltip();
    }

    // ── 内部 ──

    private void RebuildSkillItems(EnemyFollower follower)
    {
        // 清理旧技能项
        foreach (var item in _skillItems)
        {
            if (item != null && item.Root != null)
                Destroy(item.Root);
        }
        _skillItems.Clear();

        if (follower == null || _skillRoot == null || _skillItemTemplate == null) return;

        var skills = follower.SkillManager != null ? follower.SkillManager.SkillInstances : null;
        if (skills == null || skills.Count == 0) return;

        // 最多显示 3 个技能图标（面板宽度有限）
        int showCount = Mathf.Min(skills.Count, 3);

        for (int i = 0; i < showCount; i++)
        {
            SkillInstance skill = skills[i];
            if (skill == null || skill.Data == null) continue;

            GameObject item = Instantiate(_skillItemTemplate, _skillRoot);
            item.SetActive(true);

            var refObj = new SkillItemRef
            {
                Root = item,
                Skill = skill,
                Icon = item.transform.Find("Icon")?.GetComponent<Image>(),
                Overlay = item.transform.Find("Overlay")?.GetComponent<Image>(),
                CdText = item.transform.Find("CdText")?.GetComponent<TMP_Text>(),
            };

            if (refObj.Icon != null)
            {
                bool hasIcon = skill.Data.icon != null;
                refObj.Icon.sprite = hasIcon ? skill.Data.icon : _fallbackIcon;
                refObj.Icon.enabled = true;
                // 无图标时显示灰色占位圆，避免面板右侧空白
                refObj.Icon.color = hasIcon
                    ? Color.white
                    : new Color(0.6f, 0.6f, 0.65f, 0.7f);
            }
            if (refObj.CdText != null)
                refObj.CdText.gameObject.SetActive(false);

            _skillItems.Add(refObj);
        }

        UpdateCooldowns();
    }

    private static string GetDisplayName(EnemyCore core)
    {
        if (core == null) return "随从";
        if (core.config != null && !string.IsNullOrEmpty(core.config.displayName))
            return core.config.displayName;
        return core.gameObject.name;
    }
}
