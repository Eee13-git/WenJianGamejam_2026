using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 音频管理器（持久化单例）— 全局音效（SFX）与背景音乐（BGM）的统一播放入口。
///
/// 通过 [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] 自动创建，DontDestroyOnLoad 跨场景保留。
/// 音效通过 AudioClipLibrary（名称 → 音频）查询：技能施法音效键 = skillId，
/// 特殊事件键如 player_hurt / enemy_hurt / axon_block_end 等。
/// 特性：
///   - SFX 播放器池（轮询复用，支持并发音效）
///   - BGM 独立播放器（循环播放 + 停止）
///   - 主音量 / 音效音量 / 音乐音量 三级分离
///   - 同名音效最小播放间隔（防止高频触发导致噪音爆炸）
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效播放器池")]
    [Tooltip("SFX 播放器数量（并发音效上限）")]
    [SerializeField] private int _sfxSourceCount = 4;

    [Header("音乐播放器")]
    [SerializeField] private AudioSource _musicSource;

    [Header("音效库")]
    [Tooltip("名称 → AudioClip 映射库（Resources/Audio/AudioClipLibrary.asset）")]
    [SerializeField] private AudioClipLibrary _library;

    [Header("音量")]
    [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _musicVolume = 1f;

    [Header("防重叠")]
    [Tooltip("同一音效的最小播放间隔（秒），防止高频触发导致噪音爆炸")]
    [SerializeField] private float _minIntervalBetweenSameSound = 0.05f;

    private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
    private int _sfxIndex;
    private readonly Dictionary<string, float> _lastPlayedTime = new Dictionary<string, float>();

    /// <summary>主音量（0~1）</summary>
    public float MasterVolume => _masterVolume;

    /// <summary>音效音量（0~1）</summary>
    public float SfxVolume => _sfxVolume;

    /// <summary>音乐音量（0~1）</summary>
    public float MusicVolume => _musicVolume;

    /// <summary>是否正在播放音乐</summary>
    public bool IsMusicPlaying => _musicSource != null && _musicSource.isPlaying;

    // ==================== 生命周期 ====================

    /// <summary>在首个场景加载前自动创建（保证任何场景都有音频系统）</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;

        var go = new GameObject("[AudioManager]");
        go.AddComponent<AudioManager>();
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

        // 自动加载音效库（Build 可用：Resources 路径）
        if (_library == null)
            _library = Resources.Load<AudioClipLibrary>("Audio/AudioClipLibrary");

        // 创建 SFX 播放器池
        for (int i = 0; i < _sfxSourceCount; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // 2D 音效
            _sfxSources.Add(src);
        }

        // 创建音乐播放器
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
        }

        // 场景无 AudioListener 时补一个（保证有声音输出）
        EnsureAudioListener();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAudioListener();
    }

    /// <summary>保证全局恰好一个 AudioListener：场景已有则禁用自己身上的，否则启用/添加</summary>
    private void EnsureAudioListener()
    {
        var listeners = FindObjectsOfType<AudioListener>();

        bool hasOther = false;
        foreach (var l in listeners)
        {
            if (l.gameObject != gameObject)
            {
                hasOther = true;
                break;
            }
        }

        var myListener = GetComponent<AudioListener>();
        if (hasOther)
        {
            if (myListener != null) myListener.enabled = false;
        }
        else
        {
            if (myListener == null) gameObject.AddComponent<AudioListener>();
            else myListener.enabled = true;
        }
    }

    // ==================== 音效 ====================

    /// <summary>播放一次性音效（按名称查询音效库）</summary>
    public void PlaySFX(string soundName, float volume = 1f, float pitch = 1f)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        if (_library == null || !_library.TryGetEntry(soundName, out var entry))
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[AudioManager] 未找到音效条目: {soundName}");
#endif
            return;
        }

        // 同一音效最小间隔（防高频叠加，如大量敌人同时受击）
        float last;
        _lastPlayedTime.TryGetValue(soundName, out last);
        if (Time.time - last < _minIntervalBetweenSameSound) return;
        _lastPlayedTime[soundName] = Time.time;

        PlaySFX(entry.clip, entry.volume * volume, entry.pitch * pitch);
    }

    /// <summary>播放一次性音效（直接传 AudioClip）</summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || _sfxSources.Count == 0) return;

        // 轮询选择播放器（并发上限 = _sfxSourceCount）
        AudioSource src = _sfxSources[_sfxIndex];
        _sfxIndex = (_sfxIndex + 1) % _sfxSources.Count;

        src.volume = _masterVolume * _sfxVolume * Mathf.Clamp01(volume);
        src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        src.PlayOneShot(clip);
    }

    /// <summary>播放技能释放音效（按 skillId 查询；无条目则静默，不告警）</summary>
    public void PlaySkillCastSound(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        if (_library == null || !_library.TryGetEntry(skillId, out _)) return;
        PlaySFX(skillId);
    }

    // ==================== 音乐 ====================

    /// <summary>播放背景音乐（按名称查询音效库）</summary>
    public void PlayMusic(string soundName, bool loop = true)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        if (_library == null || !_library.TryGetEntry(soundName, out var entry)) return;
        PlayMusic(entry.clip, loop);
    }

    /// <summary>播放背景音乐</summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (_musicSource == null || clip == null) return;

        if (_musicSource.clip == clip && _musicSource.isPlaying) return;

        _musicSource.clip = clip;
        _musicSource.loop = loop;
        _musicSource.volume = _masterVolume * _musicVolume;
        _musicSource.Play();
    }

    /// <summary>停止背景音乐</summary>
    public void StopMusic()
    {
        if (_musicSource != null)
            _musicSource.Stop();
    }

    // ==================== 音量 ====================

    /// <summary>设置主音量（同步 AudioListener.volume，与设置面板兼容）</summary>
    public void SetMasterVolume(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        AudioListener.volume = _masterVolume;
        ApplyVolumes();
    }

    /// <summary>设置音效音量</summary>
    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
    }

    /// <summary>设置音乐音量</summary>
    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        if (_musicSource != null)
            _musicSource.volume = _masterVolume * _musicVolume;
    }

    private void ApplyVolumes()
    {
        if (_musicSource != null)
            _musicSource.volume = _masterVolume * _musicVolume;
    }
}
