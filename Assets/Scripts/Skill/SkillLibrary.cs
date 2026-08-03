using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 技能库（ScriptableObject）—— 所有技能的唯一工厂入口。
/// 右键 -> Create -> Game -> Skill Library 创建。
/// </summary>
[CreateAssetMenu(fileName = "SkillLibrary", menuName = "Game/Skill Library")]
public class SkillLibrary : ScriptableObject
{
    [SerializeField] private List<SkillData> _allSkills;

    /// <summary>所有技能列表</summary>
    public IReadOnlyList<SkillData> AllSkills => _allSkills;

    /// <summary>通过 ID 查找技能数据</summary>
    public SkillData GetById(string skillId)
    {
        return _allSkills?.Find(s => s.skillId == skillId);
    }

    /// <summary>
    /// 创建完整可用的技能实例（SkillLibrary 是唯一工厂入口）。
    /// 通过 skillEffect 策略注入 OnExecute 委托。
    /// </summary>
    public SkillInstance CreateSkillInstance(string skillId)
    {
        SkillData data = GetById(skillId);
        if (data == null)
        {
            Debug.LogWarning($"SkillLibrary: 未找到 skillId='{skillId}'");
            return null;
        }

        SkillInstance instance = new SkillInstance(data);

        instance.OnExecute += (caster, direction) =>
        {
            if (data.skillEffect == null) return;

            Projectile.OwnerType ownerType = caster.GetOwnerType();
            data.skillEffect.Execute(caster, direction,
                instance.CurrentDamageMultiplier, ownerType);
        };

        return instance;
    }

    /// <summary>随机获取一个技能数据</summary>
    public SkillData GetRandom()
    {
        if (_allSkills == null || _allSkills.Count == 0) return null;
        return _allSkills[Random.Range(0, _allSkills.Count)];
    }

    /// <summary>
    /// 随机创建一个技能实例
    /// </summary>
    public SkillInstance CreateRandomInstance()
    {
        SkillData data = GetRandom();
        return data != null ? new SkillInstance(data) : null;
    }

    /// <summary>随机获取 N 个不重复的技能数据</summary>
    public List<SkillData> GetRandomDistinct(int count)
    {
        if (_allSkills == null || _allSkills.Count == 0) return new List<SkillData>();

        var shuffled = _allSkills.OrderBy(_ => Random.value).ToList();
        int take = Mathf.Min(count, shuffled.Count);
        return shuffled.GetRange(0, take);
    }
}
