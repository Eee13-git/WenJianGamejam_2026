using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效库（ScriptableObject）— 名称 → AudioClip 的映射表。
/// 技能施法音效的键 = 技能 skillId（如 "hypertonic_lysate"）；
/// 特殊事件键如 "player_hurt" / "enemy_hurt" / "axon_block_end" /
/// "muscle_burst_wall" / "muscle_burst_normal" / "photon_radiance_landing" 等。
/// 右键 -> Create -> Game -> Audio Clip Library 创建。
/// </summary>
[CreateAssetMenu(fileName = "AudioClipLibrary", menuName = "Game/Audio Clip Library")]
public class AudioClipLibrary : ScriptableObject
{
    /// <summary>单个音效条目</summary>
    [System.Serializable]
    public class SoundEntry
    {
        [Tooltip("键名：技能 skillId 或事件名（如 player_hurt）")]
        public string name;

        [Tooltip("音频文件")]
        public AudioClip clip;

        [Tooltip("基础音量倍率")]
        [Range(0f, 2f)] public float volume = 1f;

        [Tooltip("基础音调倍率")]
        [Range(0.1f, 3f)] public float pitch = 1f;
    }

    /// <summary>音效条目列表（按名称索引）</summary>
    public List<SoundEntry> sounds = new List<SoundEntry>();

    // 查询缓存（首次访问时构建）
    private Dictionary<string, SoundEntry> _cache;

    /// <summary>按名称获取音频，找不到返回 null</summary>
    public AudioClip GetClip(string name)
    {
        return TryGetEntry(name, out var entry) ? entry.clip : null;
    }

    /// <summary>按名称获取条目</summary>
    public bool TryGetEntry(string name, out SoundEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(name)) return false;

        if (_cache == null)
        {
            _cache = new Dictionary<string, SoundEntry>();
            if (sounds != null)
            {
                foreach (var s in sounds)
                {
                    if (s != null && !string.IsNullOrEmpty(s.name))
                        _cache[s.name] = s;
                }
            }
        }

        return _cache.TryGetValue(name, out entry);
    }
}
