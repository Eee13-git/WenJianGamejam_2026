using UnityEngine;

/// <summary>
/// 召唤物存在时间上限 — 计时结束后死亡/销毁。
/// 由 SummonSkillEffect 在生成召唤物时挂载（所有召唤物统一拥有存在上限）。
/// - 有 EnemyCore：到时走正常死亡流程（TakeDamage 致死，触发亡语/掉落/房间计数移除）
/// - 无 EnemyCore（如光子光柱视觉）：到时直接销毁
/// - 被同化为随从后自动解除（随从由玩家管理复活/生命周期，不再受存在上限约束）
/// </summary>
public class SummonLifetime : MonoBehaviour
{
    private float _remaining;

    /// <summary>初始化存在时间（秒）</summary>
    public void Init(float duration)
    {
        _remaining = Mathf.Max(0f, duration);
    }

    private void Update()
    {
        if (_remaining <= 0f) return;

        var core = GetComponent<EnemyCore>();

        // 被同化为随从：解除存在上限（随从有独立生命周期/复活管理）
        if (core != null && core.IsAssimilated)
        {
            Destroy(this);
            return;
        }
        var follower = GetComponent<EnemyFollower>();
        if (follower != null && follower.IsActive)
        {
            Destroy(this);
            return;
        }

        _remaining -= Time.deltaTime;
        if (_remaining > 0f) return;

        // 到时：有 EnemyCore → 走正常死亡流程（亡语/掉落/房间计数移除）；
        // 无 EnemyCore（纯视觉召唤物如光子光柱）→ 直接销毁
        if (core != null && !core.IsDead)
            core.TakeDamage(99999f);
        else
            Destroy(gameObject);
    }
}
