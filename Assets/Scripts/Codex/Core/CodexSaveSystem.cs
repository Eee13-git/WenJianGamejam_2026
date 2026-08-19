using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 图鉴持久化数据
/// </summary>
[System.Serializable]
public class CodexSaveData
{
    public List<string> unlockedIds = new();
}

/// <summary>
/// 图鉴存档系统 — 封装 PlayerPrefs 的读写逻辑。
/// </summary>
public static class CodexSaveSystem
{
    private const string SAVE_KEY = "Codex_SaveData";

    public static void Save(HashSet<string> unlockedIds)
    {
        var data = new CodexSaveData
        {
            unlockedIds = new List<string>(unlockedIds)
        };
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    public static HashSet<string> Load()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
            return new HashSet<string>();

        string json = PlayerPrefs.GetString(SAVE_KEY);
        if (string.IsNullOrEmpty(json))
            return new HashSet<string>();

        var data = JsonUtility.FromJson<CodexSaveData>(json);
        if (data?.unlockedIds == null)
            return new HashSet<string>();

        return new HashSet<string>(data.unlockedIds);
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
    }
}
