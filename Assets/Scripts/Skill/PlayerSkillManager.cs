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
        new SkillSlot { keyBinding = KeyCode.F },
        new SkillSlot { keyBinding = KeyCode.E },
        new SkillSlot { keyBinding = KeyCode.R },
    };

    [Header("技能库")]
    [SerializeField] private SkillLibrary _skillLibrary;

    [Header("初始技能")]
    [Tooltip("游戏开始时自动装备的技能 ID（留空则不装备）")]
    [SerializeField] private string _initialSkillId;

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

    // ---------- 委托 ----------
    /// <summary>技能释放事件：参数为 (槽位索引, 技能数据)</summary>
    public event System.Action<int, SkillData> OnSkillCast;
    /// <summary>技能升级事件</summary>
    public event System.Action<int, int> OnSkillUpgraded;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        EquipInitialSkill();
    }

    private void EquipInitialSkill()
    {
        if (string.IsNullOrEmpty(_initialSkillId)) return;
        EquipSkill(0, _initialSkillId);
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

        Vector2 direction = GetTargetDirection();

        if (slot.TryCast(this, direction))
        {
            OnSkillCast?.Invoke(index, slot.Skill.Data);
        }
    }

    // ---------- 获取与装备 ----------
    /// <summary>从技能库获取技能并装备到第一个空槽</summary>
    public bool AcquireSkill(string skillId)
    {
        if (_skillLibrary == null) return false;

        SkillInstance instance = _skillLibrary.CreateSkillInstance(skillId);
        return instance != null && EquipToEmptySlot(instance);
    }

    /// <summary>装备技能到指定槽位</summary>
    public bool EquipSkill(int index, string skillId)
    {
        if (_skillLibrary == null) return false;
        if (index < 0 || index >= _slots.Count || !_slots[index].isUnlocked) return false;

        SkillInstance instance = _skillLibrary.CreateSkillInstance(skillId);
        if (instance == null) return false;

        _slots[index].Equip(instance);
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
    /// <summary>拓展技能槽数量</summary>
    public void ExpandSlots(int additionalSlots)
    {
        for (int i = 0; i < additionalSlots; i++)
        {
            _slots.Add(new SkillSlot { keyBinding = KeyCode.None, isUnlocked = true });
        }
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

    public int SlotCount => _slots.Count;
}
