using System.Collections.Generic;
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

    // === 寻路状态 ===
    private PathfindingGrid _grid;
    private List<Vector2> _cachedPath;
    private int _pathIndex;
    private Vector2 _lastPathTarget;
    private float _nextPathRecomputeTime;
    private bool _isFollowingComplexPath;

    private const float PathRecomputeInterval = 0.3f;
    private const float TargetMoveThreshold = 1f;
    private const float ArriveThreshold = 0.2f;

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

    // ==================== 寻路 API ====================

    public void SetRoomGrid(PathfindingGrid grid) { _grid = grid; }

    /// <summary>
    /// 矩形检测：以敌人为中心，沿 dir 方向检测 checkDist 距离内是否有障碍或墙。
    /// 矩形宽=敌人直径，长=checkDist。
    /// </summary>
    public bool IsDirectionBlocked(Vector2 dir, float checkDist = 0.5f)
    {
        Vector2 start = transform.position;
        Vector2 normalizedDir = dir.normalized;
        if (normalizedDir == Vector2.zero) return false;

        float enemyDiameter = GetEnemyDiameter();
        Vector2 target = start + normalizedDir * checkDist;
        Vector2 center = (start + target) / 2f;
        float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg;
        Vector2 size = new Vector2(checkDist, enemyDiameter);

        var hits = Physics2D.OverlapBoxAll(center, size, angle);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Wall") || hit.CompareTag("Obstacles") || hit.CompareTag("Spike") || hit.CompareTag("Hole"))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 矩形视线检测 — 矩形宽=敌人直径，方向朝目标。
    /// 用于寻路判断（与发现玩家的 HasLineOfSight 不同）。
    /// </summary>
    public bool HasObstacleInPath(Vector2 target)
    {
        Vector2 start = transform.position;
        Vector2 dir = target - start;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;
        dir /= dist;

        float enemyDiameter = GetEnemyDiameter();
        Vector2 center = (start + target) / 2f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 size = new Vector2(dist, enemyDiameter);

        var hits = Physics2D.OverlapBoxAll(center, size, angle);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Wall") || hit.CompareTag("Obstacles") || hit.CompareTag("Spike") || hit.CompareTag("Hole"))
                return true;
        }
        return false;
    }

    private float GetEnemyDiameter()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float w = sr.sprite.rect.width / sr.sprite.pixelsPerUnit * transform.localScale.x;
            float h = sr.sprite.rect.height / sr.sprite.pixelsPerUnit * transform.localScale.y;
            return Mathf.Max(w, h);
        }
        var col = GetComponent<Collider2D>();
        if (col != null) return Mathf.Max(col.bounds.size.x, col.bounds.size.y);
        return 1f;
    }

    /// <summary>
    /// 获取到目标的路径。无障碍=直线（单点）；有障碍=A*。
    /// _isFollowingComplexPath 锁：路径>1 时强制走完，中途不切直线。
    /// </summary>
    public List<Vector2> GetPath(Vector2 target)
    {
        // 1. 正在跟随复杂路径 → 沿用，除非目标位移过大
        if (_isFollowingComplexPath && _cachedPath != null && _cachedPath.Count > 1)
        {
            if ((target - _lastPathTarget).sqrMagnitude < TargetMoveThreshold * TargetMoveThreshold)
                return _cachedPath;
        }

        // 2. 无有效路径或目标大变 → 重新评估
        bool hasObstacle = HasObstacleInPath(target);

        if (!hasObstacle)
        {
            _cachedPath = new List<Vector2> { target };
            _pathIndex = 0;
            _isFollowingComplexPath = false;
            _lastPathTarget = target;
            return _cachedPath;
        }

        // 3. 有障碍 → A*（冷却检查）
        if (Time.time < _nextPathRecomputeTime && _cachedPath != null && _cachedPath.Count > 1)
            return _cachedPath;

        if (_grid == null)
        {
            _cachedPath = new List<Vector2> { target };
            _pathIndex = 0;
            _isFollowingComplexPath = false;
            _lastPathTarget = target;
            return _cachedPath;
        }

        var path = AStarPathfinder.FindPath(transform.position, target, _grid);
        _nextPathRecomputeTime = Time.time + PathRecomputeInterval;
        _lastPathTarget = target;

        if (path != null && path.Count > 1)
        {
            _cachedPath = path;
            _pathIndex = 0;
            _isFollowingComplexPath = true;
        }
        else
        {
            _cachedPath = new List<Vector2> { target };
            _pathIndex = 0;
            _isFollowingComplexPath = false;
        }
        return _cachedPath;
    }

    /// <summary>
    /// 沿路径点移动。距离阈值 + 投影判断双保险防过冲。
    /// </summary>
    public bool MoveAlongPath(List<Vector2> path, float speed, float speedMultiplier = 1f)
    {
        if (path == null || path.Count == 0) return true;
        if (_rb == null || speed <= 0f) return false;

        Vector2 currentPos = transform.position;
        Vector2 currentTargetPos = path[_pathIndex];
        float sqrDist = (currentPos - currentTargetPos).sqrMagnitude;

        if (sqrDist > ArriveThreshold * ArriveThreshold)
        {
            // 防过冲：如果当前路径点不是最后一个，检查是否已越过该点
            if (_pathIndex < path.Count - 1)
            {
                Vector2 nextTarget = path[_pathIndex + 1];
                Vector2 dirToNext = (nextTarget - currentTargetPos).normalized;
                float projection = Vector2.Dot(currentPos - currentTargetPos, dirToNext);
                if (projection > 0f)
                {
                    _pathIndex++;
                    return false;
                }
            }
            MoveTowardsPosition(currentTargetPos, speed, speedMultiplier);
            return false;
        }

        // 到达当前点 → 推进
        if (_pathIndex < path.Count - 1)
        {
            _pathIndex++;
            return false;
        }

        // 到达终点
        _isFollowingComplexPath = false;
        return true;
    }
}

