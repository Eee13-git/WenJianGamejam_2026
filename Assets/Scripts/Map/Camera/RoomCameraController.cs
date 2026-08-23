using System.Collections;
using UnityEngine;

/// <summary>
/// 房间摄像机控制器 — 固定房间视角 + 房间切换平滑滑动。
/// 挂载在 MainCamera 上。
/// 
/// 行为:
/// - 在房间中: 摄像机固定在房间中心，orthographicSize = 根据房间大小自动计算
/// - 房间切换: 平滑滑动到新房间中心 (0.5s)
/// - 过渡期间通知 MapManager 锁定玩家输入
/// </summary>
/// <remarks>
/// DefaultExecutionOrder 低于 CameraShake：确保本组件的 LateUpdate 先执行（锁定位置），
/// CameraShake 的 LateUpdate 随后叠加振动偏移，避免振动被覆盖。
/// </remarks>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class RoomCameraController : MonoBehaviour
{
    [Header("过渡设置")]
    [Tooltip("房间切换过渡时长 (秒)")]
    public float transitionDuration = 0.5f;

    [Tooltip("过渡曲线")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("视角设置")]
    [Tooltip("摄像机 Orthographic Size 与房间高度的比例 (1.0 = 正好包住房间内高，1.2 = 含墙壁+门边距)")]
    public float sizeScale = 1.2f;

    [Tooltip("最小 Orthographic Size")]
    public float minOrthoSize = 5f;

    [Tooltip("最大 Orthographic Size")]
    public float maxOrthoSize = 25f;

    private Camera _cam;
    private Vector3 _targetPosition;
    private float _targetOrthoSize;
    private bool _isTransitioning;
    private Coroutine _transitionCoroutine;
    private RoomRoot _currentRoom;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _targetPosition = transform.position;
        _targetOrthoSize = _cam.orthographicSize;
    }

    private void LateUpdate()
    {
        if (!_isTransitioning)
        {
            // 固定模式：直接锁定在目标位置
            transform.position = _targetPosition;
            _cam.orthographicSize = _targetOrthoSize;
        }
    }

    /// <summary>切换摄像机到目标房间</summary>
    /// <param name="room">目标房间</param>
    /// <param name="onComplete">过渡完成回调</param>
    public void MoveToRoom(RoomRoot room, System.Action onComplete = null)
    {
        if (room == null) return;

        _currentRoom = room;
        Vector3 roomCenter = room.Center;
        roomCenter.z = transform.position.z; // 保持 Z 轴

        // 计算合适的 Orthographic Size
        float targetSize = CalculateOrthoSize(room);

        if (transitionDuration <= 0f || _targetPosition == roomCenter)
        {
            // 瞬间切换
            _targetPosition = roomCenter;
            _targetOrthoSize = targetSize;
            onComplete?.Invoke();
            return;
        }

        // 平滑过渡
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionRoutine(roomCenter, targetSize, onComplete));
    }

    /// <summary>根据房间大小计算合适的 Orthographic Size。</summary>
    /// <remarks>房间视觉范围 = roomSize + 四周墙壁(0.5×2=1)。取高宽中较严的方向，确保全覆盖。</remarks>
    private float CalculateOrthoSize(RoomRoot room)
    {
        // 含墙壁的完整视觉尺寸
        float visualW = room.roomSize.x + 3f;
        float visualH = room.roomSize.y + 3f;

        // Orthographic Size = half height → 纵向覆盖需要 visualH/2
        float sizeFromHeight = visualH * 0.5f * sizeScale;
        // 横向覆盖需要 visualW/2 / aspect
        float sizeFromWidth = visualW * 0.5f * sizeScale / _cam.aspect;
        float size = Mathf.Max(sizeFromHeight, sizeFromWidth);
        return Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
    }

    /// <summary>平滑过渡协程</summary>
    private IEnumerator TransitionRoutine(Vector3 targetPos, float targetSize, System.Action onComplete)
    {
        _isTransitioning = true;
        
        Vector3 startPos = transform.position;
        float startSize = _cam.orthographicSize;

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float curveT = transitionCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPos, targetPos, curveT);
            _cam.orthographicSize = Mathf.Lerp(startSize, targetSize, curveT);

            yield return null;
        }

        // 最终值精确设置
        transform.position = targetPos;
        _cam.orthographicSize = targetSize;
        _targetPosition = targetPos;
        _targetOrthoSize = targetSize;

        _isTransitioning = false;
        onComplete?.Invoke();
    }

    /// <summary>立即跳转到房间 (无过渡)</summary>
    public void SnapToRoom(RoomRoot room)
    {
        if (room == null) return;

        _currentRoom = room;
        Vector3 roomCenter = room.Center;
        roomCenter.z = transform.position.z;
        
        transform.position = roomCenter;
        _targetPosition = roomCenter;
        _targetOrthoSize = CalculateOrthoSize(room);
        _cam.orthographicSize = _targetOrthoSize;
    }

    public RoomRoot CurrentRoom => _currentRoom;
    public bool IsTransitioning => _isTransitioning;
}
