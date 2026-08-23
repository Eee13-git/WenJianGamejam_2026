using System.Collections;
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

    [Header("释放动画延迟")]
    [Tooltip("技能效果延迟执行时间（秒），与 cast 动画中效果帧的时间对应。0=立即执行")]
    [SerializeField] private float _castDelay = 0f;

    private List<SkillInstance> _skillInstances = new List<SkillInstance>();

    public IReadOnlyList<SkillInstance> SkillInstances => _skillInstances;

    /// <summary>技能释放事件：参数为 (槽位索引, 技能数据)</summary>
    public event System.Action<int, SkillData> OnSkillCast;

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

    /// <summary>
    /// 自动执行所有被动技能的效果（不进入冷却、不触发施法动画）。
    /// 用于出生时自动生效的被动光环类技能。
    /// </summary>
    public void AutoCastPassives(ISkillCaster caster)
    {
        foreach (var s in _skillInstances)
        {
            if (s == null || s.Data == null) continue;
            if (!s.Data.passive) continue;
            s.ExecuteEffect(caster, Vector2.down);
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

    /// <summary>
    /// 从就绪技能中随机选择一个释放（用于"概率发动多技能"的敌人）。
    /// 返回是否成功释放。冷却中的技能（含被动）不会参与选择。
    /// </summary>
    public bool TryCastRandomReadySkill(ISkillCaster caster, Vector2 dir)
    {
        // 收集所有就绪的主动技能
        var ready = new System.Collections.Generic.List<int>();
        for (int i = 0; i < _skillInstances.Count; i++)
        {
            var s = _skillInstances[i];
            if (s == null || s.Data == null) continue;
            if (s.Data.passive) continue;      // 被动技能不参与随机
            if (s.IsCoolingDown) continue;
            ready.Add(i);
        }

        if (ready.Count == 0) return false;

        int idx = ready[Random.Range(0, ready.Count)];
        return TryCastSkill(idx, caster, dir);
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

    /// <summary>
    /// 刷新剩余冷却时间最长的技能（细胞部分重编程等效果使用）。
    /// 返回被刷新的技能实例；无冷却中的技能时返回 null。
    /// </summary>
    public SkillInstance ResetLongestCooldownSkill(string excludeSkillId = null)
    {
        SkillInstance longest = null;
        foreach (var s in _skillInstances)
        {
            if (s == null || !s.IsCoolingDown) continue;
            // 排除自身（施放中的技能不应刷新自己的冷却）
            if (excludeSkillId != null && s.Data.skillId == excludeSkillId) continue;
            if (longest == null || s.CooldownRemaining > longest.CooldownRemaining)
                longest = s;
        }

        if (longest != null)
            longest.ResetCooldown();
        return longest;
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
        var skill = _skillInstances[index];
        // 被动技能已通过 AutoCastPassives 自动执行，不应被主动施放
        if (skill.Data != null && skill.Data.passive) return false;
        if (skill.IsCoolingDown) return false;

        // 开始冷却 + 触发动画事件
        skill.StartCooldown();
        OnSkillCast?.Invoke(index, skill.Data);

        // 延迟执行技能效果：优先使用技能效果的"施法动画帧同步"延迟（特效出现帧），否则用 _castDelay
        float delay = _castDelay;
        if (skill.Data != null && skill.Data.skillEffect is ICastAnimationSync sync)
            delay = sync.EffectStartDelay;

        if (delay > 0f)
            StartCoroutine(DelayedExecute(skill, caster, dir, delay));
        else
        {
            PlayCastSound(skill);
            skill.ExecuteEffect(caster, dir);
        }

        return true;
    }

    private IEnumerator DelayedExecute(SkillInstance skill, ISkillCaster caster, Vector2 dir, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayCastSound(skill);
        skill.ExecuteEffect(caster, dir);
    }

    /// <summary>播放技能释放音效（与动画效果帧同步；无专属音效的技能静默）</summary>
    private void PlayCastSound(SkillInstance skill)
    {
        if (skill == null || skill.Data == null) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySkillCastSound(skill.Data.skillId);
    }
}
