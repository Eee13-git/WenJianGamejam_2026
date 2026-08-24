using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Result 场景结算控制器 —— 主屏幕仅显示标题 + 按钮；
/// 点击 "查看结算" 按钮弹出新 UI 面板，展示全部结算数据与评分明细。
///
/// 挂在 Result 场景的 Canvas 上。
/// 通过 [SerializeField] 引用 ScoreCalculator 和各 TMP_Text 组件。
/// </summary>
public class ResultSceneController : MonoBehaviour
{
    [Header("分数计算器")]
    [SerializeField] private ScoreCalculator _scoreCalculator;

    [Header("Replay / Exit 按钮")]
    [SerializeField] private Button _replayButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private string _replaySceneName = "Start";

    [Header("主屏幕标题文本")]
    [SerializeField] private TMP_Text _titleText;

    [Header("唤起结算面板的按钮")]
    [SerializeField] private Button _openSettlementButton;

    [Header("结算弹窗 (场景中预置)")]
    [SerializeField] private GameObject _settlementPanel;
    [SerializeField] private GameObject _settlementOverlay;
    [SerializeField] private Button _closeSettlementButton;
    [SerializeField] private TMP_Text _panelTitleText;
    [SerializeField] private TMP_Text _panelScoreText;
    [SerializeField] private TMP_Text _panelDetailText;

    [Header("科技点")]
    [Tooltip("分数 ÷ 此值 = 获得的科技点")]
    [SerializeField] private int _techPointDivisor = 1000;

    private bool _techPointsAwarded = false;
    private int _lastEarnedTechPoints = 0;

    private void Start()
    {
        if (_replayButton != null)
            _replayButton.onClick.AddListener(OnReplay);
        if (_exitButton != null)
            _exitButton.onClick.AddListener(OnExit);

        if (_openSettlementButton != null)
            _openSettlementButton.onClick.AddListener(OnOpenSettlement);

        if (_closeSettlementButton != null)
            _closeSettlementButton.onClick.AddListener(OnCloseSettlement);

        if (_settlementOverlay != null)
        {
            var overlayBtn = _settlementOverlay.GetComponent<Button>();
            if (overlayBtn != null)
                overlayBtn.onClick.AddListener(OnCloseSettlement);
        }

        if (_settlementPanel != null)
            _settlementPanel.gameObject.SetActive(false);
        if (_settlementOverlay != null)
            _settlementOverlay.gameObject.SetActive(false);

        DisplayTitle();
    }

    private void OnReplay()
    {
        // 重置道具池（新一局开始）
        if (ItemsLibrary.Instance != null)
            ItemsLibrary.Instance.ResetPool();

        SceneManager.LoadScene(_replaySceneName);
    }

    private void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================== 结算弹窗 ====================

    private void OnOpenSettlement()
    {
        if (_settlementPanel == null) return;

        _settlementPanel.SetActive(true);
        if (_settlementOverlay != null)
            _settlementOverlay.SetActive(true);

        // 首次打开时发放科技点
        if (!_techPointsAwarded)
        {
            AwardTechPoints();
            _techPointsAwarded = true;
        }

        StartCoroutine(DisplayPanelResultsCoroutine());
    }

    /// <summary>根据结算分数发放科技点（分数 / _techPointDivisor）</summary>
    private void AwardTechPoints()
    {
        var stats = GameStatistics.Instance;
        if (stats == null || _scoreCalculator == null) return;

        int score = _scoreCalculator.CalculateScore(stats);
        _lastEarnedTechPoints = _techPointDivisor > 0 ? score / _techPointDivisor : 0;

        if (_lastEarnedTechPoints > 0 && TechTreeManager.Instance != null)
        {
            TechTreeManager.Instance.AddTechPoints(_lastEarnedTechPoints);
        }
    }

    private System.Collections.IEnumerator DisplayPanelResultsCoroutine()
    {
        WritePanelText();
        yield return null;
        RebuildContentLayout();
        yield return null;
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>写入弹窗内的结算文本（不含布局重建）</summary>
    private void WritePanelText()
    {
        var stats = GameStatistics.Instance;
        if (stats == null || _scoreCalculator == null) return;

        int score = _scoreCalculator.CalculateScore(stats);

        if (_panelTitleText != null)
            _panelTitleText.text = "结算明细";
        if (_panelScoreText != null)
            _panelScoreText.text = $"总分: {score}";
        if (_panelDetailText != null)
            _panelDetailText.text = BuildDetailText(stats, score);
    }

    /// <summary>强制重建 ScrollView Content 的布局</summary>
    private void RebuildContentLayout()
    {
        if (_panelDetailText == null) return;

        var contentRt = _panelDetailText.rectTransform.parent as RectTransform;
        if (contentRt == null) return;

        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
    }

    private void OnCloseSettlement()
    {
        if (_settlementPanel != null)
            _settlementPanel.SetActive(false);
        if (_settlementOverlay != null)
            _settlementOverlay.SetActive(false);
    }

    // ==================== 主屏幕 (仅标题) ====================

    private void DisplayTitle()
    {
        if (_titleText != null)
        {
            var stats = GameStatistics.Instance;
            _titleText.text = "<size=100>结算</size>";
        }
    }

    // ==================== 详细文本构建 ====================

    private string BuildDetailText(GameStatistics stats, int score)
    {
        var sb = new System.Text.StringBuilder();
        var bd = _scoreCalculator != null
            ? _scoreCalculator.GetScoreBreakdown(stats)
            : default;

        // ── 道具 ──
        sb.AppendLine("<size=30><color=#8A5A00>━━━━ 获得的道具 ━━━━</color></size>");
        if (stats.AcquiredItems.Count == 0)
        {
            sb.AppendLine("  <color=#4A5E6B>· 无</color>");
        }
        else
        {
            foreach (var kvp in stats.AcquiredItems)
            {
                var item = kvp.Value.data;
                int count = kvp.Value.count;
                string qualityColor = item.quality switch
                {
                    ItemQuality.Legendary => "#B25E00",
                    ItemQuality.Rare => "#2C4F9E",
                    ItemQuality.Uncommon => "#1E7A1E",
                    _ => "#4A5E6B"
                };
                string countStr = count > 1 ? $"  ×<color=#8A5A00>{count}</color>" : "";
                int totalPrice = item.price * count;
                string priceStr = count > 1
                    ? $"<color=#8A5A00>{totalPrice} ATP</color> <color=#4A5E6B>({item.price}×{count})</color>"
                    : $"<color=#8A5A00>{item.price} ATP</color>";
                sb.AppendLine($"  <color={qualityColor}>{item.itemName}</color>  {priceStr}{countStr}");
            }
        }

        // ── 金钱 ──
        sb.AppendLine();
        sb.AppendLine("<size=30><color=#8A5A00>━━━━ 金 钱 ━━━━</color></size>");
        sb.AppendLine($"  总收入:  <color=#1E7A1E>+{stats.TotalCurrencyEarned} ATP</color>");
        sb.AppendLine($"  总支出:  <color=#B22222>-{stats.TotalCurrencySpent} ATP</color>");

        int net = stats.TotalCurrencyEarned - stats.TotalCurrencySpent;
        string netColor = net >= 0 ? "#1E7A1E" : "#B22222";
        string netSign = net >= 0 ? "+" : "";
        sb.AppendLine($"  净收入:  <color={netColor}>{netSign}{net} ATP</color>");

        // ── 探索 ──
        sb.AppendLine();
        sb.AppendLine("<size=30><color=#8A5A00>━━━━ 探 索 ━━━━</color></size>");
        AppendRoomTypeStats(sb, stats);
        sb.AppendLine($"  ── 合计 ──");
        sb.AppendLine($"  进入  <color=#0E6B9E>{stats.RoomsVisited}</color> 间  |  清空  <color=#8A5A00>{stats.RoomsCleared}</color> 间");

        // ── 击杀 ──
        sb.AppendLine();
        sb.AppendLine("<size=30><color=#8A5A00>━━━━ 击 杀 ━━━━</color></size>");
        if (stats.EnemiesKilled.Count == 0)
        {
            sb.AppendLine("  <color=#4A5E6B>· 无</color>");
        }
        else
        {
            foreach (var kvp in stats.EnemiesKilled)
            {
                bool isBoss = kvp.Key.Contains("Boss") || kvp.Key.Contains("boss");
                string enemyLabel = isBoss
                    ? $"  <color=#C0392B>[Boss] {kvp.Key}</color>"
                    : $"  <color=#4A5E6B>·</color> {kvp.Key}";
                sb.AppendLine($"{enemyLabel}  ×<color=#8A5A00>{kvp.Value}</color>");
            }
        }

        // ── 评分明细 ──
        sb.AppendLine();
        sb.AppendLine("<size=30><color=#8A5A00>━━━━ 评分明细 ━━━━</color></size>");

        if (_scoreCalculator != null)
        {
            sb.AppendLine($"  道具分:  <color=#1E7A1E>+{bd.itemScore:F0}</color>  ({bd.itemCount} 件 × {bd.itemPriceMultiplier})");
            sb.AppendLine($"  击杀分:  <color=#B25E00>+{bd.killScore:F0}</color>  ({bd.totalKills} 个敌人)");
            sb.AppendLine($"  金钱分:  <color=#8A5A00>+{bd.currencyScore:F0}</color>  (收入 {bd.currencyEarned} × {bd.currencyEarnedMultiplier})");
            if (bd.roomScore > 0)
                sb.AppendLine($"  探索分:  <color=#0E6B9E>+{bd.roomScore:F0}</color>  (进入 {bd.roomsVisited}×{bd.roomVisitedScore} + 清空 {bd.roomsCleared}×{bd.roomClearedBonus})");
            if (bd.currencyPenalty > 0)
                sb.AppendLine($"  消费罚分:  <color=#B22222>-{bd.currencyPenalty:F0}</color>  (支出 {bd.currencySpent} × {bd.currencySpentPenalty})");
        }

        // ── 科技点 ──
        sb.AppendLine();
        sb.AppendLine("<size=30><color=#8A5A00>━━━━ 科技点 ━━━━</color></size>");
        if (_lastEarnedTechPoints > 0)
        {
            sb.AppendLine($"  本局获得:  <color=#1E7A1E>+{_lastEarnedTechPoints}</color>  (总分 {score} ÷ {_techPointDivisor})");
            int total = TechTreeManager.Instance != null ? TechTreeManager.Instance.TechPoints : 0;
            sb.AppendLine($"  当前总计:  <color=#8A5A00>{total}</color>");
        }
        else
        {
            sb.AppendLine($"  <color=#4A5E6B>本局分数不足，未获得科技点 (需 {_techPointDivisor} 分)</color>");
        }

        // ── 总分 ──
        sb.AppendLine();
        sb.AppendLine($"<size=36><color=#8A5A00>══════ 总 分: {score} ══════</color></size>");

        return sb.ToString();
    }

    /// <summary>追加各房间类型的进入/清空明细</summary>
    private void AppendRoomTypeStats(System.Text.StringBuilder sb, GameStatistics stats)
    {
        // 合并 visited 和 cleared 的房间类型
        var allTypes = new HashSet<RoomType>();
        foreach (var t in stats.RoomsVisitedByType.Keys) allTypes.Add(t);
        foreach (var t in stats.RoomsClearedByType.Keys) allTypes.Add(t);

        foreach (var roomType in allTypes)
        {
            stats.RoomsVisitedByType.TryGetValue(roomType, out int visited);
            stats.RoomsClearedByType.TryGetValue(roomType, out int cleared);
            string typeName = RoomTypeToString(roomType);
            sb.AppendLine($"  {typeName}  进入 <color=#0E6B9E>{visited}</color>  清空 <color=#8A5A00>{cleared}</color>");
        }
    }

    /// <summary>RoomType 转中文名</summary>
    private static string RoomTypeToString(RoomType type)
    {
        return type switch
        {
            RoomType.Start => "出生点",
            RoomType.Normal => "普通房",
            RoomType.Treasure => "宝箱房",
            RoomType.Boss => "Boss房",
            RoomType.Shop => "商店",
            RoomType.Hidden => "隐藏房",
            _ => type.ToString()
        };
    }
}
