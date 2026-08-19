using UnityEngine;

/// <summary>
/// 图鉴解锁钩子 — 挂载在游戏场景中，监听敌人死亡/道具拾取/技能获得事件，
/// 自动解锁对应图鉴条目。
/// </summary>
public class CodexUnlockHook : MonoBehaviour
{
    private PlayerSkillManager _skillManager;

    private void OnEnable()
    {
        EnemyCore.OnAnyEnemySpawned += OnEnemySpawned;
        ItemManager.OnAnyItemAcquired += OnItemAcquired;
    }

    private void OnDisable()
    {
        EnemyCore.OnAnyEnemySpawned -= OnEnemySpawned;
        ItemManager.OnAnyItemAcquired -= OnItemAcquired;
    }

    private void Start()
    {
        var player = PlayerManager.Instance?.CurrentPlayer;
        if (player != null)
        {
            _skillManager = player.GetComponent<PlayerSkillManager>();
            if (_skillManager != null)
                _skillManager.OnSkillAcquired += OnSkillAcquired;
        }
    }

    private void OnDestroy()
    {
        if (_skillManager != null)
            _skillManager.OnSkillAcquired -= OnSkillAcquired;
    }

    private void OnEnemySpawned(EnemyCore enemy)
    {
        if (enemy?.config == null) return;
        string id = enemy.config.displayName;
        if (string.IsNullOrEmpty(id)) return;

        if (CodexManager.Instance.UnlockEntry(id))
        {
            // 新解锁 — 查找条目获取描述信息
            var entry = FindEnemyEntry(id);
            if (entry != null)
                CodexNotificationUI.ShowNotification(entry.icon, entry.displayName, entry.description);
            else
                CodexNotificationUI.ShowNotification(null, id, "");
        }
    }

    private void OnItemAcquired(GameObject owner)
    {
        if (owner == null) return;
        var im = owner.GetComponent<ItemManager>();
        if (im == null) return;

        foreach (var item in im.Items)
        {
            if (item == null) continue;
            CodexManager.Instance.UnlockEntry(item.itemId);
        }
    }

    private void OnSkillAcquired(string skillId)
    {
        CodexManager.Instance.UnlockEntry(skillId);
    }

    private CodexEnemyEntrySO FindEnemyEntry(string entryId)
    {
        var codex = CodexManager.Instance?.EnemyCodex;
        if (codex == null) return null;
        foreach (var entry in codex.EnemyEntries)
        {
            if (entry != null && entry.entryId == entryId)
                return entry;
        }
        return null;
    }
}
