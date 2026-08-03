using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WenJian.UI
{
    /// <summary>
    /// Start 场景主菜单控制器 — 处理 Play / Exit 按钮
    /// </summary>
    public class StartMenuController : MonoBehaviour
    {
        [Header("按钮引用")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;

        [Header("场景名称")]
        [SerializeField] private string playSceneName = "yang";

        private void Start()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);
        }

        private void OnPlayClicked()
        {
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

#if UNITY_EDITOR
        [ContextMenu("Test: Play")]
        private void TestPlay() => OnPlayClicked();

        [ContextMenu("Test: Exit")]
        private void TestExit() => OnExitClicked();
#endif
    }
}
