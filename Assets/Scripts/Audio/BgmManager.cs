using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 背景音乐管理器（持久化单例）— 根据场景与房间类型切换 BGM。
///
/// 自动创建（BeforeSceneLoad）+ DontDestroyOnLoad。
/// 音效键约定（AudioClipLibrary）：
///   bgm_menu             — 主界面/结算（Start / Result 场景加载时播放）
///   bgm_combat_1 / bgm_combat_2 — 日常战斗（进入普通房间时随机一首）
///   bgm_boss_1   / bgm_boss_2   — Boss 战（进入 Boss 房间时随机一首）
///
/// 切换时机：
///   - Start / Result 场景加载 → PlayMenu()（主界面音乐）
///   - RoomManager.OnPlayerEnter 进入 Boss 房间 → PlayBoss()
///   - RoomManager.OnPlayerEnter 进入其他房间 → PlayCombat()
/// </summary>
public class BgmManager : MonoBehaviour
{
    public static BgmManager Instance { get; private set; }

    [Header("BGM 音效键")]
    [Tooltip("主界面音乐（Start/Result 场景播放）")]
    [SerializeField] private string _menuMusic = "bgm_menu";
    [Tooltip("日常战斗音乐（进入普通房间时随机一首）")]
    [SerializeField] private string _combatMusic1 = "bgm_combat_1";
    [SerializeField] private string _combatMusic2 = "bgm_combat_2";
    [Tooltip("Boss 战音乐")]
    [SerializeField] private string _bossMusic1 = "bgm_boss_1";
    [SerializeField] private string _bossMusic2 = "bgm_boss_2";

    [Header("场景控制")]
    [Tooltip("在这些场景加载时播放主界面音乐（主菜单/结算）")]
    [SerializeField] private string[] _menuMusicScenes = { "Start", "Result" };

    /// <summary>当前正在播放的音乐键（无则空）</summary>
    public string CurrentPlayingKey { get; private set; } = "";

    private string _noRepeatMemory;

    // ==================== 生命周期 ====================

    /// <summary>在首个场景加载前自动创建</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;

        var go = new GameObject("[BgmManager]");
        go.AddComponent<BgmManager>();
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

        SceneManager.sceneLoaded += OnSceneLoaded;

        // 当前场景立即检查一次（首次创建时可能在任意场景）
        var scene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(scene.name))
            HandleSceneMusic(scene.name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneMusic(scene.name);
    }

    /// <summary>场景加载时的音乐策略：菜单/结算场景播放主界面音乐；游戏场景等 RoomManager 触发</summary>
    private void HandleSceneMusic(string sceneName)
    {
        foreach (var s in _menuMusicScenes)
        {
            if (sceneName == s)
            {
                PlayMenu();
                return;
            }
        }
        // 游戏场景不自动播放，等待 RoomManager.OnPlayerEnter 触发（避免多层场景切歌冲突）
    }

    // ==================== 播放接口 ====================

    /// <summary>播放主界面音乐（Start/Result 场景；若当前已在该音乐则不打断）</summary>
    public void PlayMenu()
    {
        if (AudioManager.Instance == null) return;

        if (CurrentPlayingKey == _menuMusic)
        {
            if (AudioManager.Instance.IsMusicPlaying) return;
        }

        AudioManager.Instance.PlayMusic(_menuMusic);
        CurrentPlayingKey = _menuMusic;
    }

    /// <summary>播放日常战斗音乐（随机一首；若当前已是战斗音乐则不切歌）</summary>
    public void PlayCombat()
    {
        if (AudioManager.Instance == null) return;

        // 已在播放战斗音乐 → 不打断
        if (CurrentPlayingKey == _combatMusic1 || CurrentPlayingKey == _combatMusic2)
        {
            if (AudioManager.Instance.IsMusicPlaying) return;
        }

        string key = PickRandom(_combatMusic1, _combatMusic2);
        AudioManager.Instance.PlayMusic(key);
        CurrentPlayingKey = key;
    }

    /// <summary>播放 Boss 战音乐（随机一首；若当前已是 Boss 音乐则不切歌）</summary>
    public void PlayBoss()
    {
        if (AudioManager.Instance == null) return;

        if (CurrentPlayingKey == _bossMusic1 || CurrentPlayingKey == _bossMusic2)
        {
            if (AudioManager.Instance.IsMusicPlaying) return;
        }

        string key = PickRandom(_bossMusic1, _bossMusic2);
        AudioManager.Instance.PlayMusic(key);
        CurrentPlayingKey = key;
    }

    /// <summary>停止所有音乐</summary>
    public void StopAllMusic()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();
        CurrentPlayingKey = "";
    }

    /// <summary>随机取一首（避免与上次相同）</summary>
    private string PickRandom(string a, string b)
    {
        if (a == b) return a;
        float roll = Random.value;
        string pick = roll < 0.5f ? a : b;
        if (pick == _noRepeatMemory) pick = pick == a ? b : a;
        _noRepeatMemory = pick;
        return pick;
    }
}
