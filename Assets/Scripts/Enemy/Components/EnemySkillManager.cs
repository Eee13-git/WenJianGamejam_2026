using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人的技能管理组件：负责装配技能、驱动冷却并提供 TryCast 接口。
/// 普通敌人使用自动 Update Tick；Boss 设置 _manualTick=true 后由 BossCore 手动调用。
/// </summary>
public class EnemySkillManager : MonoBehaviour
{
    [Tooltip("设为 true 则不会自动 Tick 冷却（供 BossCore 手动控制）")]
    [SerializeField] private bool _manualTick;

    private List<SkillInstance> _skillInstances = new List<SkillInstance>();

    public IReadOnlyList<SkillInstance> SkillInstances => _skillInstances;

    public void InitializeFromLibrary(SkillLibrary library, int randomMin = 1, int randomMax = 2)
    {
        _skillInstances.Clear();
        if (library == null) return;

        var datas = library.GetRandomDistinct(Random.Range(randomMin, randomMax + 1));
        foreach (var d in datas)
        {
            if (d == null) continue;
            var inst = library.CreateSkillInstance(d.skillId);
            if (inst != null) _skillInstances.Add(inst);
        }
    }

    /// <summary>
    /// 按 skillId 加载指定技能（用于预设技能列表，如 Boss 阶段技能）。
    /// 会清空当前技能列表。
    /// </summary>
    public void LoadSkills(SkillLibrary library, params string[] skillIds)
    {
        _skillInstances.Clear();
        if (library == null) return;

        foreach (var id in skillIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var inst = library.CreateSkillInstance(id);
            if (inst != null) _skillInstances.Add(inst);
        }
    }

    /// <summary>手动 Tick 冷却（供 Boss 等需要自定义 Update 的场景调用）</summary>
    public void TickCooldowns(float dt)
    {
        foreach (var s in _skillInstances)
            s.TickCooldown(dt);
    }

    /// <summary>获取第一个冷却完毕的技能实例（供 AI 选择施放）</summary>
    public SkillInstance GetReadySkill()
    {
        foreach (var s in _skillInstances)
            if (!s.IsCoolingDown) return s;
        return null;
    }

    /// <summary>获取所有冷却完毕的技能实例</summary>
    public int ReadyCount
    {
        get
        {
            int c = 0;
            foreach (var s in _skillInstances)
                if (!s.IsCoolingDown) c++;
            return c;
        }
    }

    /// <summary>按 skillId 刷新技能冷却（完美格挡等效果使用）</summary>
    public void ResetCooldownBySkillId(string skillId)
    {
        foreach (var s in _skillInstances)
        {
            if (s != null && s.Data.skillId == skillId)
            {
                s.ResetCooldown();
                return;
            }
        }
    }

    // ---------- 旧 API（保持兼容） ----------

    private void Update()
    {
        if (!_manualTick)
            TickCooldowns(Time.deltaTime);
    }

    public bool TryCastSkill(int index, ISkillCaster caster, Vector2 dir)
    {
        if (index < 0 || index >= _skillInstances.Count) return false;
        return _skillInstances[index].TryCast(caster, dir);
    }
}
