using UnityEngine;

/// <summary>
/// 移动组件：封装 Rigidbody2D 操作与视线检测缓存。
///
/// 速度数据源：EnemyStats.PatrolSpeed / EnemyStats.ChaseSpeed
///             由状态机在调用 MoveTowardsPosition 时显式传入。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Tooltip("全局敌人速度倍率（道具效果），1=正常，0.5=半速")]
    public static float GlobalSpeedMultiplier = 1f;

    private Rigidbody2D _rb;
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

    /// <summary>
    /// 向目标位置移动。
    /// </summary>
    /// <param name="target">目标世界坐标</param>
    /// <param name="speed">移动速度</param>
    /// <param name="speedMultiplier">额外速度倍率（0~1），用于搜索等减速行为</param>
    public void MoveTowardsPosition(Vector2 target, float speed, float speedMultiplier = 1f)
    {
        if (_rb == null || speed <= 0f) return;
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        _rb.velocity = dir * speed * speedMultiplier * GlobalSpeedMultiplier;
    }

    public void SetVelocity(Vector2 vel)
    {
        if (_rb == null) return;
        _rb.velocity = vel * GlobalSpeedMultiplier;
    }

    /// <summary>直接传送到目标位置</summary>
    public void Teleport(Vector2 targetPos)
    {
        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
            _rb.position = targetPos;
        }
        else
        {
            transform.position = targetPos;
        }
    }

    /// <summary>
    /// 带缓存的视线检测。
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
            _nextSightCheckTime = 0f;
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
