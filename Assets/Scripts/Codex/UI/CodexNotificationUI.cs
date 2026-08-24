using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 新敌人遭遇通知 UI — 屏幕右侧中间弹出，淡入淡出，默认5秒。
/// 支持队列：多个新敌人同时生成时依次显示。
/// 基于预制体 CodexNotification.prefab。
/// </summary>
public class CodexNotificationUI : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descText;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("动画")]
    [SerializeField] private float _duration = 5f;
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.3f;

    private static CodexNotificationUI _instance;
    private Coroutine _currentRoutine;

    private static readonly Queue<(Sprite icon, string name, string desc)> _queue = new();

    public static void ShowNotification(Sprite icon, string name, string desc)
    {
        _queue.Enqueue((icon, name, desc));
        var inst = GetInstance();
        if (inst == null) return;

        // 如果当前没有在显示，立即开始下一条
        if (inst._currentRoutine == null || !inst.gameObject.activeSelf)
            inst.StartNextNotification();
    }

    private void StartNextNotification()
    {
        if (_queue.Count == 0)
        {
            gameObject.SetActive(false);
            _currentRoutine = null;
            return;
        }

        var (icon, name, desc) = _queue.Dequeue();

        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.gameObject.SetActive(icon != null);
        }
        if (_nameText != null)
            _nameText.text = name;
        if (_descText != null)
            _descText.text = desc;

        gameObject.SetActive(true);

        if (_currentRoutine != null)
            StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(FadeRoutine());
    }

    private static CodexNotificationUI GetInstance()
    {
        if (_instance != null) return _instance;
        _instance = FindObjectOfType<CodexNotificationUI>(true);
        if (_instance == null)
        {
            var prefab = Resources.Load<GameObject>("Codex/CodexNotification");
            if (prefab == null)
            {
                Debug.LogError("[CodexNotificationUI] 预制体未找到: Resources/Codex/CodexNotification");
                return null;
            }
            var go = Instantiate(prefab);
            go.name = "CodexNotification";

            // 查找根 Canvas（避免挂到弹窗的嵌套 Canvas 下）；若无则创建专用 Canvas
            Canvas rootCanvas = null;
            foreach (var c in FindObjectsOfType<Canvas>())
            {
                if (c.isRootCanvas)
                {
                    rootCanvas = c;
                    break;
                }
            }
            if (rootCanvas != null)
            {
                go.transform.SetParent(rootCanvas.transform, false);
            }
            else
            {
                var canvasGo = new GameObject("CodexNotificationCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
                go.transform.SetParent(canvasGo.transform, false);
            }

            _instance = go.GetComponent<CodexNotificationUI>();
            if (_instance == null)
                _instance = go.AddComponent<CodexNotificationUI>();
        }
        return _instance;
    }

    private IEnumerator FadeRoutine()
    {
        // 淡入
        float fadeIn = Mathf.Max(0.01f, _fadeInDuration);
        if (_canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        // 停留
        float stay = Mathf.Max(0f, _duration - _fadeInDuration - _fadeOutDuration);
        yield return new WaitForSecondsRealtime(stay);

        // 淡出
        float fadeOut = Mathf.Max(0.01f, _fadeOutDuration);
        if (_canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }

        _currentRoutine = null;
        // 队列中还有 → 显示下一条；否则隐藏
        StartNextNotification();
    }
}
