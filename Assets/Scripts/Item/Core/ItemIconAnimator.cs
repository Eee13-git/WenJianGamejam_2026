using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具图标帧动画组件 — 在 SpriteRenderer（世界）或 Image（UI）上按 FPS 循环播放 Sprite[]。
/// 由 ItemPickup（世界）和 ItemSlotView（UI）动态挂载/移除。
/// </summary>
public class ItemIconAnimator : MonoBehaviour
{
    private Sprite[] _frames;
    private float _fps = 8f;
    private SpriteRenderer _sr;
    private Image _uiImage;
    private float _timer;
    private int _index;

    /// <summary>
    /// 初始化动画组件。
    /// </summary>
    /// <param name="frames">帧动画精灵数组</param>
    /// <param name="fps">播放速率（帧/秒）</param>
    /// <param name="isUI">true=UI Image 模式，false=SpriteRenderer 模式</param>
    public void Initialize(Sprite[] frames, float fps, bool isUI)
    {
        _frames = frames;
        _fps = fps;
        _index = 0;
        _timer = 0f;
        if (isUI)
            _uiImage = GetComponent<Image>();
        else
            _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_frames == null || _frames.Length <= 1) return;

        _timer += Time.deltaTime;
        float interval = 1f / _fps;
        if (_timer >= interval)
        {
            _timer -= interval;
            _index = (_index + 1) % _frames.Length;
            var sprite = _frames[_index];
            if (_sr != null)
                _sr.sprite = sprite;
            if (_uiImage != null)
                _uiImage.sprite = sprite;
        }
    }
}
