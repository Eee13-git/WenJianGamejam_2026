using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 图鉴管理器 (持久化单例) — 管理解锁状态、查询条目、触发解锁事件。
/// 仿 TechTreeManager：[RuntimeInitializeOnLoadMethod] + DontDestroyOnLoad。
/// </summary>
public class CodexManager : MonoBehaviour
{
    public static CodexManager Instance { get; private set; }

    private const string ENEMY_CODEX_PATH = "Codex/CodexEnemySO";
    private const string SKILL_CODEX_PATH = "Codex/CodexSkillSO";
    private const string ITEM_CODEX_PATH = "Codex/CodexItemSO";

    [Header("图鉴资产")]
    [SerializeField] private CodexEnemySO _enemyCodex;
    [SerializeField] private CodexSkillSO _skillCodex;
    [SerializeField] private CodexItemSO _itemCodex;

    private HashSet<string> _unlockedIds;

    /// <summary>条目解锁事件: (entryId)</summary>
    public event Action<string> OnEntryUnlocked;

    public CodexEnemySO EnemyCodex => _enemyCodex;
    public CodexSkillSO SkillCodex => _skillCodex;
    public CodexItemSO ItemCodex => _itemCodex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;
        var go = new GameObject("[CodexManager]");
        go.AddComponent<CodexManager>();
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

        if (_enemyCodex == null)
            _enemyCodex = Resources.Load<CodexEnemySO>(ENEMY_CODEX_PATH);
        if (_skillCodex == null)
            _skillCodex = Resources.Load<CodexSkillSO>(SKILL_CODEX_PATH);
        if (_itemCodex == null)
            _itemCodex = Resources.Load<CodexItemSO>(ITEM_CODEX_PATH);

        _unlockedIds = CodexSaveSystem.Load();
    }

    /// <summary>判断条目是否已解锁</summary>
    public bool IsUnlocked(string entryId) =>
        _unlockedIds != null && _unlockedIds.Contains(entryId);

    /// <summary>解锁条目。返回 true 表示新解锁，false 表示已解锁。</summary>
    public bool UnlockEntry(string entryId)
    {
        if (string.IsNullOrEmpty(entryId)) return false;
        if (_unlockedIds.Contains(entryId)) return false;

        _unlockedIds.Add(entryId);
        CodexSaveSystem.Save(_unlockedIds);
        OnEntryUnlocked?.Invoke(entryId);
        return true;
    }

    /// <summary>获取已解锁数量</summary>
    public int GetUnlockedCount(CodexSO codex)
    {
        if (codex == null || _unlockedIds == null) return 0;
        int count = 0;
        foreach (var entry in codex.Entries)
        {
            if (entry != null && _unlockedIds.Contains(entry.entryId))
                count++;
        }
        return count;
    }
}
