using UnityEngine;

/// <summary>
/// 临时减速组件 — 挂在被减速的敌人身上。
/// 记录原始移动速度，乘减速系数，持续一段时间后自动恢复并销毁。
/// 供干扰素等"命中减速"效果使用。
/// </summary>
public class SlowEffect : MonoBehaviour
{
    private EnemyStats _stats;
    private float _origChaseSpeed;
    private float _origPatrolSpeed;
    private float _remaining;
    private bool _applied;

    /// <summary>
    /// 施加减速。factor=0.8 表示减速20%。若已存在减速则刷新。
    /// </summary>
    public void Apply(float factor, float duration)
    {
        if (_stats == null)
            _stats = GetComponent<EnemyStats>();

        if (_stats == null)
        {
            Destroy(this);
            return;
        }

        // 首次应用时记录原始速度
        if (!_applied)
        {
            _origChaseSpeed = _stats.ChaseSpeed;
            _origPatrolSpeed = _stats.PatrolSpeed;
            _applied = true;
        }

        _stats.ChaseSpeed = _origChaseSpeed * factor;
        _stats.PatrolSpeed = _origPatrolSpeed * factor;
        _remaining = duration;
    }

    private void Update()
    {
        if (!_applied) return;

        _remaining -= Time.deltaTime;
        if (_remaining <= 0f)
        {
            // 恢复速度
            if (_stats != null)
            {
                _stats.ChaseSpeed = _origChaseSpeed;
                _stats.PatrolSpeed = _origPatrolSpeed;
            }
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (_applied && _stats != null)
        {
            _stats.ChaseSpeed = _origChaseSpeed;
            _stats.PatrolSpeed = _origPatrolSpeed;
        }
    }
}
