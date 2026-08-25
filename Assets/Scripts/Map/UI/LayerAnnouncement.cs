using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 层名大字公告 —— 进入新一层时全屏显示该层名称（淡入 → 停留 → 淡出）。
/// 由 MapManager.GenerateMap() 在生成地图后调用 LayerAnnouncement.Show(层名)。
/// 运行时动态创建独立 Canvas（ScreenSpaceOverlay，最高排序），无需场景内预制体。
/// </summary>
public class LayerAnnouncement : MonoBehaviour
{
    private static LayerAnnouncement _instance;
    private static bool _shuttingDown;

    [Header("排版")]
    [Tooltip("大字字号")]
    [SerializeField] private float _fontSize = 140f;
    [Tooltip("标题文字颜色")]
    [SerializeField] private Color _textColor = new Color(1f, 0.92f, 0.55f, 1f);
    [Tooltip("副标题前缀（如\"第 2 层\"），可留空")]
    [SerializeField] private string _subtitlePrefix = "";
    [Tooltip("副标题字号")]
    [SerializeField] private float _subtitleFontSize = 36f;
    [Tooltip("字符间距（大字标题的字与字之间间隔）")]
    [SerializeField] private float _characterSpacing = 40f;

    [Header("动画（秒）")]
    [SerializeField] private float _fadeIn = 0.6f;
    [SerializeField] private float _hold = 1.4f;
    [SerializeField] private float _fadeOut = 0.8f;

    private Canvas _canvas;
    private CanvasGroup _group;
    private TMP_Text _titleText;
    private TMP_Text _subtitleText;
    private Coroutine _activeRoutine;

    // ── 静态访问 ──

    /// <summary>显示层名公告。重复调用会重置动画。</summary>
    public static void Show(string layerName)
    {
        if (string.IsNullOrEmpty(layerName) || _shuttingDown) return;

        var inst = EnsureInstance();
        if (inst != null)
            inst.Play(layerName);
    }

    private static LayerAnnouncement EnsureInstance()
    {
        if (_instance != null) return _instance;

        var go = new GameObject("[LayerAnnouncement]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LayerAnnouncement>();
        _instance.BuildUI();
        return _instance;
    }

    private void OnApplicationQuit()
    {
        _shuttingDown = true;
    }

    // ── 构建 UI ──

    private void BuildUI()
    {
        // 独立 Canvas：全屏覆盖、最高排序，不受场景 UICanvas 影响
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var font = TMP_Settings.defaultFontAsset;

        // 副标题（第 N 层）
        var subGo = CreateText("Subtitle", font, _subtitleFontSize, new Color(0.8f, 0.8f, 0.8f, 1f),
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.62f));
        _subtitleText = subGo.GetComponent<TMP_Text>();

        // 主标题大字（层名）
        var titleGo = CreateText("Title", font, _fontSize, _textColor,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.48f));
        _titleText = titleGo.GetComponent<TMP_Text>();
    }

    private GameObject CreateText(string name, TMP_FontAsset font, float fontSize, Color color,
        TextAlignmentOptions align, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1600f, 220f);
        rt.anchoredPosition = anchoredPos;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.characterSpacing = _characterSpacing;   // 字间距（大字标题等间距排布）
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.text = "";
        return go;
    }

    // ── 播放动画 ──

    private void Play(string layerName)
    {
        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        _titleText.text = layerName;
        _subtitleText.text = _subtitlePrefix;

        _activeRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // 淡入
        float t = 0f;
        while (t < _fadeIn)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / _fadeIn);
            yield return null;
        }

        // 停留
        yield return new WaitForSecondsRealtime(_hold);

        // 淡出
        t = 0f;
        while (t < _fadeOut)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = 1f - Mathf.Clamp01(t / _fadeOut);
            yield return null;
        }

        _group.alpha = 0f;
        _activeRoutine = null;
    }
}
