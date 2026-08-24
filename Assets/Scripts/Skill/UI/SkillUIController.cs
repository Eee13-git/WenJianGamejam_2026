using UnityEngine;

/// <summary>
/// 技能 UI Controller —— 监听 PlayerSkillManager（Model），通知 SkillUIPanel（View）。
/// 本类是 Model ↔ View 的唯一耦合点。
/// </summary>
public class SkillUIController : MonoBehaviour
{
    [Header("Model 引用")]
    [SerializeField] private PlayerSkillManager _skillManager;

    [Header("View 引用")]
    [SerializeField] private SkillUIPanel _panel;

    private SkillViewData[] _previousData = System.Array.Empty<SkillViewData>();

    // ==================== 生命周期 ====================

    private void Start()
    {
        // 跨场景后序列化引用失效，动态查找 PlayerSkillManager
        if (_skillManager == null)
            _skillManager = ResolveSkillManager();

        if (_skillManager == null)
        {
            Debug.LogError("SkillUIController: PlayerSkillManager 未赋值", this);
            return;
        }
        if (_panel == null)
        {
            Debug.LogError("SkillUIController: SkillUIPanel 未赋值", this);
            return;
        }

        _panel.Initialize(_skillManager.SlotCount);
        _previousData = new SkillViewData[_skillManager.SlotCount];

        // 订阅 Model 事件
        _skillManager.OnSkillCast += HandleSkillCast;
        _skillManager.OnSkillUpgraded += HandleSkillUpgraded;

        // 初始全量刷新
        for (int i = 0; i < _skillManager.SlotCount; i++)
            SyncSlot(i);
    }

    private void Update()
    {
        if (_skillManager == null) return;

        int slotCount = _skillManager.SlotCount;

        // 槽位可能在运行时被扩展（ExpandSlots），先确保数据数组与 UI 视图就绪，避免越界
        if (slotCount > _previousData.Length)
        {
            int oldLen = _previousData.Length;
            System.Array.Resize(ref _previousData, slotCount);
            for (int i = oldLen; i < slotCount; i++)
                _previousData[i] = default;
            _panel.EnsureSlotCount(slotCount);
        }

        // 每帧同步冷却变化（冷却值通过 SkillInstance 的公开属性读取）
        for (int i = 0; i < slotCount; i++)
            SyncSlot(i);
    }

    private void OnDestroy()
    {
        if (_skillManager != null)
        {
            _skillManager.OnSkillCast -= HandleSkillCast;
            _skillManager.OnSkillUpgraded -= HandleSkillUpgraded;
        }
    }

    // ==================== Model 事件回调 ====================

    private void HandleSkillCast(int index, SkillData data) => SyncSlot(index);
    private void HandleSkillUpgraded(int index, int newLevel) => SyncSlot(index);

    // ==================== 核心同步 ====================

    private void SyncSlot(int index)
    {
        SkillInstance skill = _skillManager.GetSkill(index);
        SkillViewData data = BuildViewData(index, skill);

        if (!data.Equals(_previousData[index]))
        {
            _previousData[index] = data;
            _panel.RefreshSlot(index, data);
        }
    }

    private SkillViewData BuildViewData(int index, SkillInstance skill)
    {
        SkillViewData data = default;

        data.IsUnlocked = _skillManager.IsSlotUnlocked(index);
        data.IsEquipped = skill != null;
        data.ShowKeyLabel = true;

        // 快捷键标签
        KeyCode key = _skillManager.GetKeyCode(index);
        data.KeyLabel = KeyCodeToString(key);

        if (skill != null)
        {
            SkillData def = skill.Data;
            data.Icon = def.icon;
            data.Level = skill.Level;
            data.MaxLevel = skill.MaxLevel;
            data.IsCoolingDown = skill.IsCoolingDown;
            data.CooldownPercent = skill.CooldownPercent;
            data.CooldownRemaining = skill.CooldownRemaining;

            // 悬停 tooltip 数据
            data.SkillName = def.skillName;
            data.Description = def.description;
        }

        return data;
    }

    private static string KeyCodeToString(KeyCode key)
    {
        // 提取字母/数字部分，去除 "Alpha" 前缀
        string s = key.ToString();
        if (s.StartsWith("Alpha"))
            return s.Substring(5);
        return s;
    }

    /// <summary>运行时动态查找 PlayerSkillManager (解决跨场景序列化引用失效问题)</summary>
    private static PlayerSkillManager ResolveSkillManager()
    {
        // 优先从 PlayerManager 获取持久化 Player
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
            return PlayerManager.Instance.CurrentPlayer.GetComponent<PlayerSkillManager>();

        // 兜底: Tag 查找
        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<PlayerSkillManager>() : null;
    }
}
