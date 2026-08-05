using UnityEngine;

/// <summary>
/// 评分明细结构体 — 各分项的分数及计算参数描述
/// </summary>
public struct ScoreBreakdown
{
    public float itemScore;
    public int itemCount;
    public float itemPriceMultiplier;

    public float killScore;
    public int totalKills;
    public float killBaseScore;
    public float bossKillBonus;

    public float currencyScore;
    public int currencyEarned;
    public float currencyEarnedMultiplier;

    public float currencyPenalty;
    public int currencySpent;
    public float currencySpentPenalty;

    public float roomScore;
    public int roomsVisited;
    public int roomsCleared;
    public float roomVisitedScore;
    public float roomClearedBonus;

    public float baseScore;
    public int totalScore;
}

/// <summary>
/// 分数计算器 (ScriptableObject) — Inspector 可配置各项权重。
/// 右键 -> Create -> Game -> Score Calculator 创建。
/// </summary>
[CreateAssetMenu(menuName = "Game/Score Calculator", fileName = "ScoreCalculator")]
public class ScoreCalculator : ScriptableObject
{
    [Header("道具分数")]
    [Tooltip("每 1 ATP 道具价格 = 多少分")]
    public float itemPriceMultiplier = 2f;

    [Header("击杀分数")]
    [Tooltip("每个普通敌人击杀基础分")]
    public float killBaseScore = 100f;
    [Tooltip("Boss 击杀额外加分（名字包含 'Boss' 时触发）")]
    public float bossKillBonus = 500f;

    [Header("金钱分数")]
    [Tooltip("每获得 1 ATP = 多少分")]
    public float currencyEarnedMultiplier = 0.5f;
    [Tooltip("每消费 1 ATP 的惩罚分")]
    public float currencySpentPenalty = 0.3f;

    [Header("房间分数")]
    [Tooltip("每进入一个房间")]
    public float roomVisitedScore = 50f;
    [Tooltip("每清空一个房间 (额外)")]
    public float roomClearedBonus = 100f;

    [Header("基础分")]
    [Tooltip("保底基础分")]
    public float baseScore = 0f;

    // ==================== 计算 ====================

    /// <summary>根据统计数据计算总分</summary>
    public int CalculateScore(GameStatistics stats)
    {
        if (stats == null) return 0;
        return GetScoreBreakdown(stats).totalScore;
    }

    /// <summary>获取评分明细（含各分项分数及计算参数）</summary>
    public ScoreBreakdown GetScoreBreakdown(GameStatistics stats)
    {
        var bd = new ScoreBreakdown
        {
            baseScore = baseScore,
            itemPriceMultiplier = itemPriceMultiplier,
            killBaseScore = killBaseScore,
            bossKillBonus = bossKillBonus,
            currencyEarnedMultiplier = currencyEarnedMultiplier,
            currencySpentPenalty = currencySpentPenalty,
            roomVisitedScore = roomVisitedScore,
            roomClearedBonus = roomClearedBonus
        };

        if (stats == null)
        {
            bd.totalScore = 0;
            return bd;
        }

        float total = baseScore;

        // 道具分: Σ(item.price × count × itemPriceMultiplier)
        bd.itemScore = 0f;
        bd.itemCount = 0;
        foreach (var kvp in stats.AcquiredItems)
        {
            bd.itemScore += kvp.Value.data.price * kvp.Value.count * itemPriceMultiplier;
            bd.itemCount += kvp.Value.count;
        }
        total += bd.itemScore;

        // 击杀分: Σ(killCount × baseScore) + Boss击杀 × bossBonus
        bd.killScore = 0f;
        bd.totalKills = 0;
        foreach (var kvp in stats.EnemiesKilled)
        {
            float perKill = kvp.Key.Contains("Boss") || kvp.Key.Contains("boss")
                ? killBaseScore + bossKillBonus
                : killBaseScore;
            bd.killScore += kvp.Value * perKill;
            bd.totalKills += kvp.Value;
        }
        total += bd.killScore;

        // 金钱分
        bd.currencyEarned = stats.TotalCurrencyEarned;
        bd.currencyScore = stats.TotalCurrencyEarned * currencyEarnedMultiplier;
        total += bd.currencyScore;

        bd.currencySpent = stats.TotalCurrencySpent;
        bd.currencyPenalty = stats.TotalCurrencySpent * currencySpentPenalty;
        total -= bd.currencyPenalty;

        // 房间分
        bd.roomsVisited = stats.RoomsVisited;
        bd.roomsCleared = stats.RoomsCleared;
        bd.roomScore = stats.RoomsVisited * roomVisitedScore + stats.RoomsCleared * roomClearedBonus;
        total += bd.roomScore;

        bd.totalScore = Mathf.Max(0, Mathf.RoundToInt(total));
        return bd;
    }
}
