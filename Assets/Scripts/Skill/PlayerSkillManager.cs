using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家技能管理器，挂载在 Player GameObject 上。
/// 管理技能槽位、键位绑定、技能获取/升级/拓展。
/// </summary>
public class PlayerSkillManager : MonoBehaviour, ISkillCaster
{
    [Header("技能槽位")]
    [SerializeField] private List<SkillSlot> _slots = new()
    {
        new SkillSlot { keyBinding = KeyCode.Q },
        new SkillSlot { keyBinding = KeyCode.E },
        new SkillSlot { keyBinding = KeyCode.Z },
        new SkillSlot { keyBinding = KeyCode.X },
    };

    [Header("技能库")]
    [SerializeField] private SkillLibrary _skillLibrary;

    [Header("初始技能")]
    [Tooltip("游戏开始时自动装备的技能 ID（槽位 0~3，留空则不装备）")]
    [SerializeField] private string _initialSkillId0;
    [SerializeField] private string _initialSkillId1;
    [SerializeField] private string _initialSkillId2;
    [SerializeField] private string _initialSkillId3;

    [Header("伤害修正")]
    [Tooltip("技能伤害修正乘区（道具/buff 可修改），1.0=无修正")]
    [SerializeField] private float _skillDamageMultiplier = 1f;

    /// <summary>全局技能伤害修正乘区（道具效果，如 cAMP），1=正常</summary>
    public static float SkillDamageBonusMultiplier = 1f;

    /// <summary>技能伤害修正乘区，外部可读</summary>
    public float SkillDamageMultiplier => _skillDamageMultiplier;

    // ---------- ISkillCaster ----------
    public Transform CasterTransform => transform;
    private PlayerStats _stats;

    public Vector2 GetTargetDirection()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;
        return (mouseWorldPos - transform.position).normalized;
    }

    public float GetAttackStrength() => _stats != null ? _stats.AttackStrength : 10f;

    public Projectile.OwnerType GetOwnerType() => Projectile.OwnerType.Player;

    public float GetSkillDamageModifier()
    {
        float evolveBonus = _stats != null
            ? _stats.EvolutionTendency * _stats.EvolveSkillDamageFactor
            : 0f;
        return _skillDamageMultiplier * (1f + evolveBonus) * SkillDamageBonusMultiplier;
    }

    // ---------- 委托 ----------
    /// <summary>技能释放事件：参数为 (槽位索引, 技能数据)</summary>
    public event System.Action<int, SkillData> OnSkillCast;
    /// <summary>技能升级事件</summary>
    public event System.Action<int, int> OnSkillUpgraded;
    /// <summary>技能获得事件: (skillId)</summary>
    public event System.Action<string> OnSkillAcquired;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        EquipInitialSkills();
    }

    private void EquipInitialSkills()
    {
        var ids = new[] { _initialSkillId0, _initialSkillId1, _initialSkillId2, _initialSkillId3 };
        for (int i = 0; i < ids.Length; i++)
        {
            if (!string.IsNullOrEmpty(ids[i]))
                EquipSkill(i, ids[i]);
        }
    }

    void Update()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            SkillSlot slot = _slots[i];
            slot.Skill?.TickCooldown(Time.deltaTime);

            if (slot.isUnlocked && Input.GetKeyDown(slot.keyBinding))
            {
                CastSkill(i);
            }
        }
    }

    // ---------- 释放 ----------
    private void CastSkill(int index)
    {
        if (index < 0 || index >= _slots.Count) return;
        SkillSlot slot = _slots[index];
        if (!slot.isUnlocked || slot.IsEmpty) return;

        // 被动技能（如吞噬的敌人亡语技能）不应通过按键重复触发
        if (slot.Skill.Data != null && slot.Skill.Data.passive) return;

        Vector2 direction = GetTargetDirection();

        if (slot.TryCast(this, direction))
        {
            OnSkillCast?.Invoke(index, slot.Skill.Data);

            // 技能释放音效（与效果同步；无专属音效的技能静默）
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySkillCastSound(slot.Skill.Data.skillId);
        }
    }

    // ---------- 获取与装备 ----------
    /// <summary>从技能库获取技能并装备到第一个空槽</summary>
    public bool AcquireSkill(string skillId)
    {
        if (_skillLibrary == null) return false;

        SkillInstance instance = _skillLibrary.CreateSkillInstance(skillId);
        bool success = instance != null && EquipToEmptySlot(instance);
        if (success)
            OnSkillAcquired?.Invoke(skillId);
        return success;
    }

    /// <summary>装备技能到指定槽位</summary>
    public bool EquipSkill(int index, string skillId)
    {
        if (_skillLibrary == null) return false;
        if (index < 0 || index >= _slots.Count || !_slots[index].isUnlocked) return false;

        SkillInstance instance = _skillLibrary.CreateSkillInstance(skillId);
        if (instance == null) return false;

        _slots[index].Equip(instance);
        OnSkillAcquired?.Invoke(skillId);
        return true;
    }

    /// <summary>
    /// 用 SkillData 直接装备到指定槽位（支持玩家技能库之外的技能，如吞噬的敌人专用技能）。
    /// </summary>
    public bool EquipSkillData(int index, SkillData data)
    {
        if (_skillLibrary == null) return false;
        if (data == null) return false;
        if (index < 0 || index >= _slots.Count || !_slots[index].isUnlocked) return false;

        SkillInstance instance = _skillLibrary.CreateSkillInstance(data);
        if (instance == null) return false;

        _slots[index].Equip(instance);
        OnSkillAcquired?.Invoke(data.skillId);
        return true;
    }

    /// <summary>装备到第一个空槽</summary>
    public bool EquipToEmptySlot(SkillInstance skill)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].isUnlocked && _slots[i].IsEmpty)
            {
                _slots[i].Equip(skill);
                return true;
            }
        }
        return false;
    }

    // ---------- 升级 ----------
    /// <summary>升级指定槽位技能</summary>
    public bool UpgradeSkill(int index)
    {
        if (index < 0 || index >= _slots.Count) return false;
        SkillSlot slot = _slots[index];
        if (slot.IsEmpty) return false;

        if (slot.Skill.TryUpgrade())
        {
            OnSkillUpgraded?.Invoke(index, slot.Skill.Level);
            return true;
        }
        return false;
    }

    // ---------- 槽位管理 ----------
    /// <summary>
    /// 拓展技能槽数量。新增槽位默认绑定数字键，从 1 开始依次递增（1,2,...,9,0）。
    /// </summary>
    public void ExpandSlots(int additionalSlots)
    {
        for (int i = 0; i < additionalSlots; i++)
        {
            int newIndex = _slots.Count; // 新槽位索引（0-based）
            _slots.Add(new SkillSlot { keyBinding = GetDefaultKeyForSlot(newIndex), isUnlocked = true });
        }
    }

    /// <summary>
    /// 槽位默认键位：前 4 个由 Inspector 配置（Q/E/Z/X），第 5 个及以后的扩展槽位
    /// 按数字键 1→2→...→9→0 顺序分配。
    /// </summary>
    private static KeyCode GetDefaultKeyForSlot(int slotIndex)
    {
        if (slotIndex < 4) return KeyCode.None; // 初始槽位由 Inspector 配置

        int digit = slotIndex - 4 + 1; // 第5个槽(索引4)→数字1
        if (digit <= 9) return KeyCode.Alpha1 + (digit - 1); // Alpha1..Alpha9 连续枚举
        if (digit == 10) return KeyCode.Alpha0;              // 第14个槽(索引13)→数字0
        return KeyCode.None;
    }

    /// <summary>解锁指定槽位</summary>
    public void UnlockSlot(int index)
    {
        if (index >= 0 && index < _slots.Count)
            _slots[index].isUnlocked = true;
    }

    // ---------- 键位 ----------
    /// <summary>重新绑定槽位键位</summary>
    public void RebindKey(int index, KeyCode newKey)
    {
        if (index >= 0 && index < _slots.Count)
            _slots[index].keyBinding = newKey;
    }

    /// <summary>获取槽位技能（供外部查询，如UI）</summary>
    public SkillInstance GetSkill(int index)
    {
        return (index >= 0 && index < _slots.Count) ? _slots[index].Skill : null;
    }

    /// <summary>按 skillId 刷新技能冷却（完美格挡等效果使用）</summary>
    public void ResetCooldownBySkillId(string skillId)
    {
        foreach (var slot in _slots)
        {
            if (slot.Skill != null && slot.Skill.Data.skillId == skillId)
            {
                slot.Skill.ResetCooldown();
                return;
            }
        }
    }

    /// <summary>
    /// 刷新剩余冷却时间最长的技能（细胞部分重编程等效果使用）。
    /// 返回被刷新的技能实例；无冷却中的技能时返回 null。
    /// </summary>
    public SkillInstance ResetLongestCooldownSkill(string excludeSkillId = null)
    {
        SkillInstance longest = null;
        foreach (var slot in _slots)
        {
            var skill = slot.Skill;
            if (skill == null || !skill.IsCoolingDown) continue;
            // 排除自身（施放中的技能不应刷新自己的冷却）
            if (excludeSkillId != null && skill.Data.skillId == excludeSkillId) continue;
            if (longest == null || skill.CooldownRemaining > longest.CooldownRemaining)
                longest = skill;
        }

        if (longest != null)
            longest.ResetCooldown();
        return longest;
    }

    /// <summary>按 skillId 查找绑定键位（长按技能等效果使用）</summary>
    public KeyCode GetKeyCodeBySkillId(string skillId)
    {
        foreach (var slot in _slots)
        {
            if (slot.Skill != null && slot.Skill.Data.skillId == skillId)
                return slot.keyBinding;
        }
        return KeyCode.None;
    }

    /// <summary>查询槽位是否解锁（供 UI Controller 读取）</summary>
    public bool IsSlotUnlocked(int index)
    {
        return index >= 0 && index < _slots.Count && _slots[index].isUnlocked;
    }

    /// <summary>获取槽位绑定的快捷键（供 UI Controller 读取）</summary>
    public KeyCode GetKeyCode(int index)
    {
        if (index < 0 || index >= _slots.Count) return KeyCode.None;
        return _slots[index].keyBinding;
    }

    public int SlotCount => _slots.Count;
}
