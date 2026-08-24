using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WenJian.UI
{
    /// <summary>
    /// Start 场景主菜单控制器 — 处理 Play / Exit / 科技树 按钮
    /// </summary>
    public class StartMenuController : MonoBehaviour
    {
        [Header("按钮引用")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;

        [Header("科技树按钮")]
        [SerializeField] private Button _techTreeButton;

        [Header("图鉴按钮")]
        [SerializeField] private Button _codexButton;

        [Header("场景名称")]
        [SerializeField] private string playSceneName = "Lymphatic";

        private void Start()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            if (_techTreeButton != null)
                _techTreeButton.onClick.AddListener(OnTechTreeClicked);

            if (_codexButton != null)
                _codexButton.onClick.AddListener(OnCodexClicked);
        }

        private void OnPlayClicked()
        {
            // 重置道具池（新一局开始）
            if (ItemsLibrary.Instance != null)
                ItemsLibrary.Instance.ResetPool();

            SceneManager.LoadScene(playSceneName);
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnTechTreeClicked()
        {
            TechTreeUIController.Toggle();
        }

        private void OnCodexClicked()
        {
            CodexUIController.Toggle();
        }

#if UNITY_EDITOR
        [ContextMenu("Test: Play")]
        private void TestPlay() => OnPlayClicked();

        [ContextMenu("Test: Exit")]
        private void TestExit() => OnExitClicked();

        [ContextMenu("Test: Tech Tree")]
        private void TestTechTree() => OnTechTreeClicked();
#endif
    }
}
