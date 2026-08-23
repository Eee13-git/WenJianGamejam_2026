using UnityEngine;

/// <summary>
/// 游戏场景视差背景 — 世界空间 SpriteRenderer，跟随摄像机移动并产生微弱视差。
/// sortingOrder 设为极低值确保渲染在地板(-2)和墙壁(0)后面。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class GameParallaxBackground : MonoBehaviour
{
    [Tooltip("视差强度：0=完全跟随相机，1=完全静止。值越大背景移动越慢")]
    [SerializeField] private float _parallaxFactor = 0.15f;

    [Tooltip("背景在 Z 轴上的位置（应在相机前方、游戏对象后方）")]
    [SerializeField] private float _zPosition = 5f;

    private Transform _cam;
    private Vector3 _lastCamPos;
    private float _textureUnitSizeX;
    private float _textureUnitSizeY;

    private void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = -100; // 确保在所有游戏 sprite 后面

        // 计算 sprite 在世界中的尺寸（用于无限循环判定）
        if (sr.sprite != null)
        {
            _textureUnitSizeX = sr.sprite.texture.width / sr.sprite.pixelsPerUnit;
            _textureUnitSizeY = sr.sprite.texture.height / sr.sprite.pixelsPerUnit;
        }
    }

    private void Start()
    {
        _cam = Camera.main != null ? Camera.main.transform : null;
        if (_cam != null)
        {
            _lastCamPos = _cam.position;
            // 初始位置跟随相机
            var pos = _cam.position;
            pos.z = _zPosition;
            transform.position = pos;
        }
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            if (_cam == null) return;
            _lastCamPos = _cam.position;
        }

        // 视差偏移：相机移动量 × (1 - parallaxFactor)
        Vector3 deltaMovement = _cam.position - _lastCamPos;
        transform.position += new Vector3(deltaMovement.x * (1f - _parallaxFactor), deltaMovement.y * (1f - _parallaxFactor), 0f);

        _lastCamPos = _cam.position;

        // 确保 Z 轴固定
        var pos = transform.position;
        pos.z = _zPosition;
        transform.position = pos;
    }
}
