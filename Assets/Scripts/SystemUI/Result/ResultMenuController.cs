using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WenJian.UI
{
    /// <summary>
    /// Start �������˵������� �� ���� Play / Exit ��ť
    /// </summary>
    public class ResultMenuController : MonoBehaviour
    {
        [Header("��ť����")]
        [SerializeField] private Button replayButton;
        [SerializeField] private Button resultExitButton;

        [Header("��������")]
        [SerializeField] private string replaySceneName = "Start";

        private void Start()
        {
            if (replayButton != null)
                replayButton.onClick.AddListener(OnReplayClicked);

            if (resultExitButton != null)
                resultExitButton.onClick.AddListener(OnResultExitClicked);
        }

        private void OnReplayClicked()
        {
            // 重置道具池（新一局开始）
            if (ItemsLibrary.Instance != null)
                ItemsLibrary.Instance.ResetPool();

            SceneManager.LoadScene(replaySceneName);
        }

        private void OnResultExitClicked()
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
        private void TestExit() => OnResultExitClicked();
#endif
    }
}
