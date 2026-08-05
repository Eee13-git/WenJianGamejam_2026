using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 相机受击反馈 — 屏幕振动 + 全屏红闪。
/// 挂载在 MainCamera 上（与 RoomCameraController 共存）。
///
/// 振动: LateUpdate 中在 RoomCameraController 锁定位置的基础上叠加随机偏移，
///       偏移随衰减时间逐渐归零，不影响房间切换逻辑。
/// 红闪: 相机子对象上的全屏红色 Image（Screen Space - Overlay），受击时快速淡入淡出。
/// </summary>
/// <remarks>
/// DefaultExecutionOrder 高于 RoomCameraController(100)：RoomCameraController 先锁定相机位置，
/// 本组件的 LateUpdate 随后叠加振动偏移，振动结束后由 RoomCameraController 重新锁定。
/// </remarks>
[DefaultExecutionOrder(200)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("振动")]
    [Tooltip("振动持续时间（秒）")]
    public float shakeDuration = 0.18f;

    [Tooltip("振动最大幅度（世界单位）")]
    public float shakeMagnitude = 0.35f;

    [Tooltip("振动频率（每秒抖动次数）")]
    public float shakeFrequency = 40f;

    [Header("红闪")]
    [Tooltip("红闪最大透明度")]
    [SerializeField] private float _flashAlpha = 0.45f;

    [Tooltip("红闪淡出时间（秒）")]
    [SerializeField] private float _flashFadeDuration = 0.35f;

    private float _shakeTimeRemaining;
    private float _shakeMagnitudeNow;
    private float _shakePhase;
    private Vector3 _basePosition;
    private bool _hasBasePosition;

    private Image _flashImage;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this);

        _basePosition = transform.position;
        _hasBasePosition = true;

        // 查找或创建红闪 Image
        _flashImage = GetComponentInChildren<Image>(true);
        if (_flashImage == null)
        {
            var flashGo = new GameObject("DamageFlash");
            flashGo.transform.SetParent(transform, false);
            _flashImage = flashGo.AddComponent<Image>();
            _flashImage.color = new Color(1f, 0f, 0f, 0f);
            _flashImage.raycastTarget = false;

            // 全屏覆盖：相机近裁剪面稍前方
            var rt = _flashImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localPosition = new Vector3(0f, 0f, 1f);
            rt.localScale = Vector3.one;
        }
        _flashImage.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        if (_shakeTimeRemaining <= 0f)
            return;

        // 基于锁定位置叠加随机偏移
        float t = 1f - (_shakeTimeRemaining / shakeDuration);
        float decay = Mathf.Lerp(1f, 0f, t); // 随时间衰减

        _shakePhase += Time.deltaTime * shakeFrequency;
        float intensity = _shakeMagnitudeNow * decay;

        Vector3 offset = Random.insideUnitCircle * intensity;
        Vector3 pos = transform.position;
        pos.x += offset.x;
        pos.y += offset.y;
        transform.position = pos;

        _shakeTimeRemaining -= Time.deltaTime;
        if (_shakeTimeRemaining <= 0f)
        {
            // 振动结束，回到基准位置（RoomCameraController 下一帧重新锁定）
            if (_hasBasePosition)
                transform.position = _basePosition;
        }
    }

    /// <summary>触发屏幕振动（可指定幅度倍率）</summary>
    public void Shake(float magnitudeMultiplier = 1f)
    {
        if (_shakeTimeRemaining <= 0f)
            _basePosition = transform.position;

        _shakeTimeRemaining = Mathf.Max(_shakeTimeRemaining, shakeDuration);
        _shakeMagnitudeNow = shakeMagnitude * magnitudeMultiplier;
    }

    /// <summary>触发全屏红闪</summary>
    public void FlashRed()
    {
        if (_flashImage == null) return;

        _flashImage.enabled = true;
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _flashFadeDuration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(_flashAlpha, 0f, elapsed / _flashFadeDuration);
            _flashImage.color = new Color(1f, 0f, 0f, a);
            yield return null;
        }
        _flashImage.color = new Color(1f, 0f, 0f, 0f);
        _flashImage.enabled = false;
    }
}
