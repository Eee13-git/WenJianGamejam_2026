using UnityEngine;

namespace WenJian.UI
{
    /// <summary>
    /// 强制摄像机保持固定宽高比（默认16:9），在非16:9屏幕上以黑边补偿。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class FixedAspectRatio : MonoBehaviour
    {
        [SerializeField] private float _targetAspect = 16f / 9f;

        private Camera _cam;
        private float _lastAspect = -1f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            ApplyAspect();
        }

        private void Update()
        {
            if (!Mathf.Approximately(_cam.aspect, _targetAspect))
                ApplyAspect();
        }

        private void ApplyAspect()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _cam.aspect = _targetAspect;
            _lastAspect = _targetAspect;
        }
    }
}
