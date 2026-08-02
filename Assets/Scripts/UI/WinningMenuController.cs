using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WenJian.UI
{
    /// <summary>
    /// Start 场景主菜单控制器 — 处理 Play / Exit 按钮
    /// </summary>
    public class WinningMenuController : MonoBehaviour
    {
        [Header("按钮引用")]
        [SerializeField] private Button replayButton;
        [SerializeField] private Button winningExitButton;

        [Header("场景名称")]
        [SerializeField] private string replaySceneName = "Start";

        private void Start()
        {
            if (replayButton != null)
                replayButton.onClick.AddListener(OnReplayClicked);

            if (winningExitButton != null)
                winningExitButton.onClick.AddListener(OnWinningExitClicked);
        }

        private void OnReplayClicked()
        {
            SceneManager.LoadScene(replaySceneName);
        }

        private void OnWinningExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Test: Replay")]
        private void TestPlay() => OnReplayClicked();

        [ContextMenu("Test: Exit")]
        private void TestExit() => OnWinningExitClicked();
#endif
    }
}
