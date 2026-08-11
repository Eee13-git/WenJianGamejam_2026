using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕边缘渐晕 Buff — 永久生效，屏幕四周变暗。
/// 泛用组件，可通过参数配置颜色和柔和度。
/// </summary>
[CreateAssetMenu(fileName = "ScreenVignetteBuff", menuName = "Game/Buff Effect/Screen Vignette")]
public class ScreenVignetteBuff : BuffEffectBase
{
    [Header("渐晕配置")]
    [SerializeField] private Color _vignetteColor = new Color(0f, 0f, 0f, 1f);
    [Range(0f, 0.5f)]
    [SerializeField] private float _vignetteSoftness = 0.35f;
    [Tooltip("渐晕强度倍率，可超过1.0使边缘更黑")]
    [SerializeField] private float _intensity = 1f;

    private GameObject _vignetteObject;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        // 重置状态（防 SO 残留）
        _vignetteObject = null;

        CreateVignette();
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (_vignetteObject != null)
        {
            Object.Destroy(_vignetteObject);
            _vignetteObject = null;
        }
    }

    private void CreateVignette()
    {
        _vignetteObject = new GameObject("[Vignette]");
        _vignetteObject.layer = 5; // UI

        var canvas = _vignetteObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = _vignetteObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _vignetteObject.AddComponent<GraphicRaycaster>();

        var imageGo = new GameObject("VignetteImage");
        imageGo.transform.SetParent(_vignetteObject.transform, false);
        imageGo.layer = 5;

        var image = imageGo.AddComponent<Image>();
        image.sprite = CreateVignetteSprite();
        image.color = Color.white;
        image.raycastTarget = false;

        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Object.DontDestroyOnLoad(_vignetteObject);
    }

    private Sprite CreateVignetteSprite()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / Mathf.Sqrt(2f);
        float innerRadius = maxDist * (1f - _vignetteSoftness);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(p, center);
                float t = Mathf.InverseLerp(innerRadius, maxDist, dist);
                float alpha = Mathf.Clamp01(t) * _vignetteColor.a * _intensity;
                tex.SetPixel(x, y, new Color(_vignetteColor.r, _vignetteColor.g, _vignetteColor.b, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
