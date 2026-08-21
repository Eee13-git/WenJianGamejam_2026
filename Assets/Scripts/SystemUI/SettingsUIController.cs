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

    [Header("场景名称")]
    [SerializeField] private string _startSceneName = "Start";

    private bool _isOpen = false;

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
        Time.timeScale = 1f;
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
