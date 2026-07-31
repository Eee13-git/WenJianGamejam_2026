using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人的技能管理组件：负责装配技能、驱动冷却并提供 TryCast 接口。
/// </summary>
public class EnemySkillManager : MonoBehaviour
{
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

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var s in _skillInstances)
            s.TickCooldown(dt);
    }

    public bool TryCastSkill(int index, ISkillCaster caster, Vector2 dir)
    {
        if (index < 0 || index >= _skillInstances.Count) return false;
        return _skillInstances[index].TryCast(caster, dir);
    }
}
