using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 新手教程管理器 — 全局单例，负责上下文提示条的展示与去重。
/// - DontDestroyOnLoad，跨场景存活
/// - PlayerPrefs 记录已展示的提示（"Tutorial_{tipId}"），新一局/重开不重复
/// - 提示条：屏幕下方半透明条 + TMP 文本，淡入淡出、排队显示
/// 外部触发：ShowTip(tipId, message) 首次自动展示；ShowTipForce(message) 强制展示（不记录）。
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    private const string PREFS_PREFIX = "Tutorial_";

    // ── 提示条 UI（运行时动态创建）──
    private Canvas _tipCanvas;
    private GameObject _tipRoot;
    private Image _tipBg;
    private TMP_Text _tipText;

    // ── 展示队列（多个提示排队显示）──
    private readonly Queue<(string message, float duration)> _queue = new();
    private bool _isShowing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;
        var go = new GameObject("[TutorialManager]");
        go.AddComponent<TutorialManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateTipUI();
    }

    /// <summary>创建屏幕下方提示条 UI（独立 ScreenSpaceOverlay Canvas，最顶层）</summary>
    private void CreateTipUI()
    {
        var canvasGo = new GameObject("TutorialTipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _tipCanvas = canvasGo.GetComponent<Canvas>();
        _tipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _tipCanvas.sortingOrder = 999; // 最顶层，盖过所有 UI

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 提示条背景：底部居中半透明条
        _tipRoot = new GameObject("TipBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _tipRoot.transform.SetParent(canvasGo.transform, false);
        var rt = _tipRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(900f, 64f);

        _tipBg = _tipRoot.GetComponent<Image>();
        _tipBg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        _tipBg.raycastTarget = false;

        // 文本
        var textGo = new GameObject("TipText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(_tipRoot.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(20f, 8f);
        textRt.offsetMax = new Vector2(-20f, -8f);

        _tipText = textGo.GetComponent<TextMeshProUGUI>();
        _tipText.font = TMP_Settings.defaultFontAsset;
        _tipText.fontSize = 22f;
        _tipText.color = Color.white;
        _tipText.alignment = TextAlignmentOptions.Center;
        _tipText.enableWordWrapping = true;
        _tipText.raycastTarget = false;

        // 初始隐藏
        var cg = _tipRoot.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
        _tipRoot.SetActive(false);
    }

    // ==================== 公开 API ====================

    /// <summary>
    /// 展示上下文提示（首次自动显示，之后不再显示）。
    /// </summary>
    /// <param name="tipId">提示唯一 ID（如 "erode_first"）</param>
    /// <param name="message">提示内容</param>
    /// <param name="duration">显示时长（秒）</param>
    /// <returns>是否展示了（false=已看过或未激活）</returns>
    public bool ShowTip(string tipId, string message, float duration = 4f)
    {
        if (string.IsNullOrEmpty(tipId)) return false;
        if (IsShown(tipId)) return false;
        MarkShown(tipId);
        ShowTipForce(message, duration);
        return true;
    }

    /// <summary>强制展示提示（不记录，可重复触发）</summary>
    public void ShowTipForce(string message, float duration = 4f)
    {
        if (string.IsNullOrEmpty(message)) return;
        _queue.Enqueue((message, Mathf.Max(duration, 1.5f)));
        if (!_isShowing)
            StartCoroutine(ProcessQueue());
    }

    /// <summary>是否已展示过该提示</summary>
    public bool IsShown(string tipId) => PlayerPrefs.HasKey(PREFS_PREFIX + tipId);

    /// <summary>标记提示已展示</summary>
    public void MarkShown(string tipId) => PlayerPrefs.SetInt(PREFS_PREFIX + tipId, 1);

    // ==================== 展示队列 ====================

    private IEnumerator ProcessQueue()
    {
        _isShowing = true;
        while (_queue.Count > 0)
        {
            var (message, duration) = _queue.Dequeue();
            yield return ShowTipRoutine(message, duration);
        }
        _isShowing = false;
    }

    private IEnumerator ShowTipRoutine(string message, float duration)
    {
        _tipRoot.SetActive(true);
        var cg = _tipRoot.GetComponent<CanvasGroup>();
        _tipText.text = message;

        // 淡入
        float fadeIn = 0.25f;
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / fadeIn);
            yield return null;
        }
        cg.alpha = 1f;

        // 停留（unscaled 时间，暂停菜单等不影响）
        yield return new WaitForSecondsRealtime(duration);

        // 淡出
        float fadeOut = 0.3f;
        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(1f - elapsed / fadeOut);
            yield return null;
        }
        cg.alpha = 0f;
        _tipRoot.SetActive(false);
    }
}
