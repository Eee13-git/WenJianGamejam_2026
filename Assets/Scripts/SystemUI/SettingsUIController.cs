using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 设置 UI 控制器 — 挂在 UICanvas/SettingsUI 节点上。
/// 提供音量、亮度调节，返回开始界面，退出游戏功能。
/// </summary>
public class SettingsUIController : MonoBehaviour
{
    [Header("按钮引用")]
    [SerializeField] private Button _settingsButton;

    [Header("面板引用")]
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private Button _dimOverlayButton;

    [Header("音量")]
    [SerializeField] private Slider _volumeSlider;

    [Header("亮度")]
    [SerializeField] private Slider _brightnessSlider;
    [SerializeField] private Image _brightnessOverlay;

    [Header("功能按钮")]
    [SerializeField] private Button _returnButton;
    [SerializeField] private Button _exitButton;

    [Header("操作说明")]
    [SerializeField] private Button _tutorialButton;
    [SerializeField] private GameObject _tutorialPanel;

    [Header("场景名称")]
    [SerializeField] private string _startSceneName = "Start";

    private bool _isOpen = false;

    private void Awake()
    {
        // 提升为独立根画布：嵌套在 UICanvas 下的子画布 sortingOrder 被 Unity 忽略，
        // 必须 SetParent(null) 成为根画布后 sortingOrder 才真正生效——
        // 保证设置界面显示在最上层，高于各类 UI 与弹窗（弹窗 sortingOrder=100）。
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            // 无窗口/后台保存会重驱动为 WorldSpace，运行时强制 ScreenSpaceOverlay 并置顶
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            if (transform.parent != null)
            {
                transform.SetParent(null);
                transform.SetAsLastSibling();
            }
        }
    }

    private void Start()
    {
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(Toggle);

        if (_dimOverlayButton != null)
            _dimOverlayButton.onClick.AddListener(Close);

        if (_volumeSlider != null)
        {
            _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            _volumeSlider.value = AudioManager.Instance != null
                ? AudioManager.Instance.MasterVolume
                : AudioListener.volume;
        }

        if (_brightnessSlider != null)
        {
            _brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
            _brightnessSlider.value = 1f;
        }

        if (_returnButton != null)
            _returnButton.onClick.AddListener(OnReturnClicked);

        if (_exitButton != null)
            _exitButton.onClick.AddListener(OnExitClicked);

        if (_tutorialButton != null)
            _tutorialButton.onClick.AddListener(OnTutorialClicked);

        Close();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        _isOpen = true;
        if (_settingsPanel != null) _settingsPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Close()
    {
        _isOpen = false;
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_tutorialPanel != null) _tutorialPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    /// <summary>操作说明手册开关（打开时保持暂停）</summary>
    private void OnTutorialClicked()
    {
        if (_tutorialPanel == null) return;
        _tutorialPanel.SetActive(!_tutorialPanel.activeSelf);
    }

    private void OnVolumeChanged(float value)
    {
        // 统一走 AudioManager 主音量（内部同步 AudioListener.volume）
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);
        else
            AudioListener.volume = value;
    }

    private void OnBrightnessChanged(float value)
    {
        // value=1 → 最亮(alpha=0), value=0 → 最暗(alpha=0.8)
        if (_brightnessOverlay != null)
        {
            var color = _brightnessOverlay.color;
            color.a = (1f - value) * 0.8f;
            _brightnessOverlay.color = color;
        }
    }

    private void OnReturnClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(_startSceneName);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
