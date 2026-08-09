using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动情腺溢散 buff 效果 — 混淆房间内敌人的攻击目标，使其互相攻击。
/// OnApply — 收集房间所有存活敌人，把每个敌人的 PlayerTarget 设为其相邻敌人
///           （A→B→C→…→A 循环链），敌人会去追击并攻击彼此。
/// OnRemove — 恢复所有敌人的 PlayerTarget 指向玩家。
/// 泛用"混乱/自相残杀"类 debuff：可作为范围混乱技能的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "ConfusionBuff", menuName = "Game/Buff Effect/Gland Confusion")]
public class ConfusionBuff : BuffEffectBase
{
    /// <summary>记录被混乱的敌人及其原目标（用于恢复）</summary>
    private static readonly Dictionary<BuffInstance, List<EnemyConfusionRecord>> _records
        = new Dictionary<BuffInstance, List<EnemyConfusionRecord>>();

    private class EnemyConfusionRecord
    {
        public EnemyCore enemy;
        public Transform originalTarget;
    }

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        // 收集房间所有存活敌人（排除死亡/同化/Boss）
        var enemies = Object.FindObjectsOfType<EnemyCore>();
        var candidates = new List<EnemyCore>();
        foreach (var e in enemies)
        {
            if (e == null || e.IsDead || e.IsAssimilated) continue;
            if (e.GetComponent<BossCore>() != null) continue;
            candidates.Add(e);
        }

        if (candidates.Count < 2)
        {
            Debug.Log("[GlandConfusion] 敌人不足 2 个，无法混淆");
            return;
        }

        var records = new List<EnemyConfusionRecord>();

        // 构建循环链：A→B→C→…→A（每个敌人的目标是下一个敌人）
        for (int i = 0; i < candidates.Count; i++)
        {
            var enemy = candidates[i];
            var next = candidates[(i + 1) % candidates.Count];

            var record = new EnemyConfusionRecord
            {
                enemy = enemy,
                originalTarget = enemy.PlayerTarget
            };

            // 设置互为目标（下一个敌人）
            enemy.PlayerTarget = next.transform;

            records.Add(record);
        }

        _records[buff] = records;
        Debug.Log($"[GlandConfusion] 混淆 {records.Count} 个敌人互相攻击");
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (!_records.TryGetValue(buff, out var records)) return;

        // 恢复所有敌人目标指向玩家
        var player = GameObject.FindGameObjectWithTag("Player");
        foreach (var record in records)
        {
            if (record.enemy == null) continue;
            record.enemy.PlayerTarget = player != null ? player.transform : record.originalTarget;
        }

        _records.Remove(buff);
        Debug.Log("[GlandConfusion] 敌人目标已恢复");
    }
}
