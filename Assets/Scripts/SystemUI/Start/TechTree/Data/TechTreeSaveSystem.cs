using UnityEngine;

/// <summary>
/// 科技树存档系统 — 封装 PlayerPrefs 的读写逻辑。
/// 负责 TechTreeSaveData 的 JSON 序列化、持久化和清除。
/// </summary>
public static class TechTreeSaveSystem
{
    private const string SAVE_KEY = "TechTree_SaveData";

    /// <summary>保存到 PlayerPrefs</summary>
    public static void Save(TechTreeSaveData data)
    {
        if (data == null) return;
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    /// <summary>从 PlayerPrefs 加载，无数据时返回空实例</summary>
    public static TechTreeSaveData Load()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
            return new TechTreeSaveData();

        string json = PlayerPrefs.GetString(SAVE_KEY);
        if (string.IsNullOrEmpty(json))
            return new TechTreeSaveData();

        var data = JsonUtility.FromJson<TechTreeSaveData>(json);
        return data ?? new TechTreeSaveData();
    }

    /// <summary>清除存档</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
    }
}
