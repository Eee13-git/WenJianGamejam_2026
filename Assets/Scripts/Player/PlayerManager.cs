using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家管理器 (持久化单例) — 负责动态生成 Player 并跨场景保留。
///
/// 通过 [RuntimeInitializeOnLoadMethod] 在首个场景加载前自动创建，
/// 通过 SceneManager.sceneLoaded 事件在每次场景加载时检测：
/// - 场景中有 Player（Tag="Player"）→ 标记 DontDestroyOnLoad 保留
/// - 场景中无 Player → 从 Player 预制体动态生成
///
/// 依赖: 项目中存在 Tag="Player" 的 Player 预制体 (Assets/Prefab/Player.prefab)
/// 被 MapManager / EnemyCore 通过 GameObject.FindGameObjectWithTag("Player") 自动发现
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player 预制体")]
    [SerializeField] private GameObject _playerPrefab;

    [Header("出生位置")]
    [SerializeField] private Vector3 _spawnPosition = Vector3.zero;

    /// <summary>场景加载后触发: 参数为 (Player GameObject)</summary>
    public event System.Action<GameObject> OnPlayerReady;

    /// <summary>当前 Player GameObject 引用</summary>
    public GameObject CurrentPlayer { get; private set; }

    // ==================== 生命周期 ====================

    /// <summary>
    /// 在首个场景加载前自动创建 PlayerManager GameObject。
    /// 确保在任何场景加载时 PlayerManager 都已在 DontDestroyOnLoad 中就绪。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;

        var go = new GameObject("[PlayerManager]");
        go.AddComponent<PlayerManager>();
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

        // Editor: 自动查找 Player 预制体，免去手动配置
        // Build: 需将 Player.prefab 放入 Resources/ 目录
        if (_playerPrefab == null)
        {
            _playerPrefab = ResolvePlayerPrefab();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ==================== 场景加载回调 ====================

    /// <summary>每次场景加载后触发，确保场景中存在 Player</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Start 场景: 销毁 Player 和统计数据
        if (scene.name == "Start")
        {
            if (CurrentPlayer != null)
            {
                Destroy(CurrentPlayer);
                CurrentPlayer = null;
            }
            DestroyStatistics();
            return;
        }

        // Result / Winning 结算场景: 销毁 Player，但保留统计数据供 UI 展示
        if (scene.name == "Result" || scene.name == "Winning")
        {
            if (CurrentPlayer != null)
            {
                Destroy(CurrentPlayer);
                CurrentPlayer = null;
            }
            return;
        }

        // === 游戏场景 ===

        // 确保 GameStatistics 存在
        if (GameStatistics.Instance == null)
        {
            var statsGo = new GameObject("[GameStatistics]");
            statsGo.AddComponent<GameStatistics>();
        }

        var player = GameObject.FindGameObjectWithTag("Player");

        // 已存在 Player —— 优先复用（可能是场景中手动放置的，或是上一场景保留的）
        if (player != null)
        {
            // 首次注册: 标记为 DontDestroyOnLoad 以便跨场景保留
            if (player != CurrentPlayer)
            {
                CurrentPlayer = player;
                DontDestroyOnLoad(player);

                // 跨场景复用时不保留当前位置，重置
                player.transform.position = Vector3.zero;

#if UNITY_EDITOR
                Debug.Log($"[PlayerManager] 复用场景中已有的 Player: '{player.name}', 位置={Vector3.zero}");
#endif
            }
            player.transform.position = Vector3.zero;
        }
        // 无 Player —— 从预制体动态生成
        else
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerManager] _playerPrefab 未配置！请在 Inspector 中指定 Player 预制体。");
                return;
            }

            var spawned = Instantiate(_playerPrefab, _spawnPosition, Quaternion.identity);
            spawned.name = "Player";
            DontDestroyOnLoad(spawned);
            CurrentPlayer = spawned;

            // 应用 PlayerConfig 基础值 + 科技树局外加成
            ApplyPlayerConfig(spawned);

#if UNITY_EDITOR
            Debug.Log($"[PlayerManager] 动态生成 Player，位置={spawned.transform.position}");
#endif
        }

        // 绑定 Player 到统计系统
        GameStatistics.Instance?.BindPlayer(CurrentPlayer);

        // 角色使用独立图层（Player=9）：与随从（Follower=8）不碰撞，物理上互不推挤
        if (CurrentPlayer != null)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                CurrentPlayer.layer = playerLayer;
                foreach (Transform child in CurrentPlayer.transform)
                    child.gameObject.layer = playerLayer;
            }
        }

        // 进化倾向头顶指示器：玩家就绪时自动挂载（跨场景复用/重新生成均覆盖）
        if (CurrentPlayer != null && CurrentPlayer.GetComponent<TendencyIndicator>() == null)
            CurrentPlayer.AddComponent<TendencyIndicator>();

        OnPlayerReady?.Invoke(CurrentPlayer);
    }

    /// <summary>销毁统计数据</summary>
    private static void DestroyStatistics()
    {
        if (GameStatistics.Instance != null)
        {
            if (Application.isPlaying)
                Destroy(GameStatistics.Instance.gameObject);
            else
                DestroyImmediate(GameStatistics.Instance.gameObject);
        }
    }

    // ==================== 辅助 ====================

    /// <summary>应用 PlayerConfig 基础值 + 科技树局外加成（仅在动态生成 Player 时调用）</summary>
    private void ApplyPlayerConfig(GameObject player)
    {
        if (player == null) return;

        var stats = player.GetComponent<PlayerStats>();
        if (stats == null) return;

        // 加载 PlayerConfig
        var config = Resources.Load<PlayerConfig>("PlayerConfig");
        if (config != null)
        {
            stats.ApplyBaseStats(config);
        }
        else
        {
            Debug.LogWarning("[PlayerManager] 未找到 PlayerConfig，Player 将使用预制体上的默认值。路径: Resources/PlayerConfig");
        }

                    // 叠加科技树加成
                    var mgr = TechTreeManager.Instance;
                    if (mgr != null)
                    {
                        var bonuses = mgr.GetAllBonuses();
                        if (bonuses != null && bonuses.Count > 0)
                        {
                            stats.ApplyBonuses(bonuses);
        #if UNITY_EDITOR
                            Debug.Log($"[PlayerManager] 已应用科技树加成: {bonuses.Count} 项");
        #endif
                        }

                        // 应用待执行的技能槽扩展
                        while (TechTreeMechanismEffect.ConsumePendingSkillSlotExpand())
                        {
                            var skillMgr = player.GetComponent<PlayerSkillManager>();
                            if (skillMgr != null)
                            {
                                skillMgr.ExpandSlots(1);
                                Debug.Log("[PlayerManager] 技能槽位 +1（科技树）");
                            }
                        }
                    }
    }

    /// <summary>自动查找 Player 预制体引用</summary>
    private static GameObject ResolvePlayerPrefab()
    {
#if UNITY_EDITOR
        // Editor 模式: 通过 AssetDatabase 按路径加载
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (prefab != null)
        {
            Debug.Log("[PlayerManager] 已自动加载 Player 预制体: Assets/Prefab/Player.prefab");
            return prefab;
        }
        Debug.LogWarning("[PlayerManager] 未找到 Assets/Prefab/Player.prefab，请确认路径正确。");
#endif

        // Build 模式: 从 Resources 目录加载
        var resourcePrefab = Resources.Load<GameObject>("Player");
        if (resourcePrefab != null)
        {
            Debug.Log("[PlayerManager] 已从 Resources 加载 Player 预制体。");
            return resourcePrefab;
        }

        Debug.LogError(
            "[PlayerManager] 无法找到 Player 预制体！\n" +
            "  - Editor: 请确认 Assets/Prefab/Player.prefab 存在\n" +
            "  - Build: 请将 Player.prefab 放入任意 Resources/ 目录下");
        return null;
    }
}
