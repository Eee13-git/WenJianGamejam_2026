using UnityEngine;

/// <summary>
/// 移动组件：封装 Rigidbody2D 操作与视线检测缓存。
///
/// 速度数据源： EnemyConfig.patrolSpeed / EnemyConfig.chaseSpeed
///             由状态机在 Enter 时写入 MoveSpeed，组件本身不做初始化。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    /// <summary>运行时当前速度（由状态机从 EnemyConfig 赋值，不在 Inspector 设置）</summary>
    [HideInInspector]
    public float MoveSpeed = 2f;

    private Rigidbody2D _rb;
    private float _sightCheckInterval = 0.2f;
    private float _nextSightCheckTime;
    private bool _cachedHasLineOfSight;
    private bool _wasInDetectionRange = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null) _rb.freezeRotation = true;
    }

    public void Stop()
    {
        if (_rb != null) _rb.velocity = Vector2.zero;
    }

    public void MoveTowardsPosition(Vector2 target, float speedMultiplier = 1f)
    {
        if (_rb == null) return;
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        _rb.velocity = dir * MoveSpeed * speedMultiplier;
    }

    public void SetVelocity(Vector2 vel)
    {
        if (_rb == null) return;
        _rb.velocity = vel;
    }

    /// <summary>
    /// 带缓存的视线检测。调用时会基于 interval 减少 Linecast 次数。
    /// </summary>
    public bool HasLineOfSight(Vector2 targetPosition, float detectionRange, float sightCheckInterval = 0.2f)
    {
        float sqrDistance = ((Vector2)transform.position - targetPosition).sqrMagnitude;
        bool inRange = sqrDistance <= detectionRange * detectionRange;

        if (!inRange)
        {
            _wasInDetectionRange = false;
            _cachedHasLineOfSight = false;
            return false;
        }

        if (!_wasInDetectionRange)
        {
            _nextSightCheckTime = 0f; // force immediate check
            _wasInDetectionRange = true;
        }

        if (Time.time >= _nextSightCheckTime)
        {
            _nextSightCheckTime = Time.time + sightCheckInterval;
            RaycastHit2D hit = Physics2D.Linecast(transform.position, targetPosition);
            _cachedHasLineOfSight = hit.collider == null || !hit.collider.CompareTag("Wall");
        }

        return _cachedHasLineOfSight;
    }
}
