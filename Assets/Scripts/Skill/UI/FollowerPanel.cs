using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// 随从列表栏 —— 挂在 UICanvas 根下的持久面板（StatsPanel 下方）。
/// 每行（FollowerAvatarView）左头像 + 右技能冷却；悬停头像延迟弹出随从详细属性 Tooltip。
/// 行数据按 EnemyFollower.Revision 重建，冷却每帧刷新。
/// </summary>
public class FollowerPanel : MonoBehaviour
{
    public static FollowerPanel Instance { get; private set; }

    [Header("UI 引用（预制体中预设）")]
    [SerializeField] private RectTransform _container;
    [SerializeField] private FollowerAvatarView _rowPrefab;

    [Header("悬停 Tooltip")]
    [SerializeField] private GameObject _tooltip;
    [SerializeField] private TMP_Text _tooltipText;
    [Tooltip("悬停多少秒后弹出详情（秒）")]
    [SerializeField] private float _tooltipDelay = 0.3f;

    private int _lastRevision = -1;
    private readonly List<FollowerAvatarView> _rows = new();
    private FollowerAvatarView _hoveredView;
    private float _hoverTimer;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (_tooltip != null)
            _tooltip.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // 随从增删/升级 → 重建行
        if (EnemyFollower.Revision != _lastRevision)
        {
            _lastRevision = EnemyFollower.Revision;
            RebuildRows();
        }

        // 冷却每帧刷新
        foreach (var row in _rows)
            row?.UpdateCooldowns();

        // 悬停延迟弹出 Tooltip
        if (_hoveredView != null && (_tooltip == null || !_tooltip.activeSelf))
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (_hoverTimer >= _tooltipDelay)
                ShowTooltipNow(_hoveredView.BoundFollower);
        }
    }

    // ── 行管理 ──

    private void RebuildRows()
    {
        HideTooltip();

        foreach (var row in _rows)
        {
            if (row != null)
                Destroy(row.gameObject);
        }
        _rows.Clear();

        if (_container == null || _rowPrefab == null) return;

        var slots = EnemyFollower.GetSlotsInOrder();
        for (int i = 0; i < slots.Count; i++)
        {
            var follower = slots[i];
            if (follower == null) continue;

            var row = Instantiate(_rowPrefab, _container);
            row.Refresh(follower);
            _rows.Add(row);
        }
    }

    // ── Tooltip ──

    /// <summary>行项悬停进入：记录悬停目标并开始延迟计时</summary>
    public void ShowTooltip(FollowerAvatarView view)
    {
        _hoveredView = view;
        _hoverTimer = 0f;
    }

    /// <summary>行项悬停离开：立即隐藏</summary>
    public void HideTooltip()
    {
        _hoveredView = null;
        _hoverTimer = 0f;
        if (_tooltip != null)
            _tooltip.SetActive(false);
    }

    private void ShowTooltipNow(EnemyFollower follower)
    {
        if (follower == null || _tooltipText == null) return;

        _tooltipText.text = BuildTooltipText(follower);

        if (_tooltip != null)
            _tooltip.SetActive(true);
    }

    private static string BuildTooltipText(EnemyFollower follower)
    {
        var core = follower != null ? follower.Core : null;
        var stats = core != null ? core.Health : null;

        string name = "随从";
        if (core != null)
        {
            if (core.config != null && !string.IsNullOrEmpty(core.config.displayName))
                name = core.config.displayName;
            else
                name = core.gameObject.name;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<b>{name}</b>  Lv.{follower.Level}");

        if (stats != null)
        {
            sb.AppendLine($"生命：{stats.CurrentHealth:F0} / {stats.MaxHealth:F0}");
            sb.AppendLine($"接触伤害：{stats.ContactDamage:F0}");
            sb.AppendLine($"移速：{stats.PatrolSpeed:F1} / {stats.ChaseSpeed:F1}");
            sb.AppendLine($"感知/攻击：{stats.DetectionRange:F1} / {stats.AttackRange:F1}");
        }

        var skills = follower.SkillManager != null ? follower.SkillManager.SkillInstances : null;
        if (skills != null && skills.Count > 0)
        {
            sb.AppendLine("技能：");
            foreach (var s in skills)
            {
                if (s == null || s.Data == null) continue;

                string cd;
                if (s.IsCoolingDown)
                    cd = $"（冷却 {s.CooldownRemaining:F1}s）";
                else if (s.Data.passive)
                    cd = "（被动）";
                else
                    cd = "（就绪）";

                sb.AppendLine($"  {s.Data.skillName} Lv.{s.Level}{cd}");
            }
        }

        return sb.ToString();
    }
}
