using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 下一层出口 — Boss/Exit 房间清空后出现。
/// 玩家触碰后加载指定场景。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NextLevelExit : MonoBehaviour
{
    [Tooltip("目标场景名 (在 Build Settings 中)")]
    public string nextSceneName;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (other.GetComponent<PlayerStats>() == null) return;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("NextLevelExit: nextSceneName 为空");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"场景 '{nextSceneName}' 未在 Build Settings 中添加或启用，无法加载！");
            return; // 直接返回，不会触发报错
        }

        // 确保 Player 在跨场景切换时不被销毁 (与 PlayerManager 协同工作)
        DontDestroyOnLoad(other.gameObject);

        SceneManager.LoadScene(nextSceneName);
    }
}
