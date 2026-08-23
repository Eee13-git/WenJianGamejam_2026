using UnityEngine;

namespace WenJian.UI
{
    /// <summary>
    /// Start 场景视差背景 — 各层以不同速率跟随鼠标位移
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        [System.Serializable]
        public class ParallaxLayer
        {
            public RectTransform rectTransform;
            [Range(0f, 1f)] public float parallaxFactor = 0.5f;

            [HideInInspector] public Vector2 baseAnchoredPosition;
        }

        [SerializeField] private ParallaxLayer[] _layers;
        [SerializeField] private float _maxOffsetX = 40f;
        [SerializeField] private float _maxOffsetY = 25f;
        [SerializeField] private float _smoothSpeed = 5f;

        private Vector2 _currentOffset;

        void Awake()
        {
            // 记录每层在 Inspector 中设置的初始 anchoredPosition
            // 后续 Update 在此基础上叠加视差偏移，不会覆盖自定义位置（如标题层偏上）
            foreach (var layer in _layers)
            {
                if (layer.rectTransform != null)
                    layer.baseAnchoredPosition = layer.rectTransform.anchoredPosition;
            }
        }

        void Update()
        {
            Vector2 mouse = Input.mousePosition;
            float nx = Mathf.Clamp((mouse.x / Screen.width - 0.5f) * 2f, -1f, 1f);
            float ny = Mathf.Clamp((mouse.y / Screen.height - 0.5f) * 2f, -1f, 1f);

            Vector2 target = new Vector2(nx * _maxOffsetX, ny * _maxOffsetY);
            _currentOffset = Vector2.Lerp(_currentOffset, target, Time.deltaTime * _smoothSpeed);

            foreach (var layer in _layers)
            {
                if (layer.rectTransform == null) continue;
                layer.rectTransform.anchoredPosition = layer.baseAnchoredPosition - _currentOffset * layer.parallaxFactor;
            }
        }
    }
}
