/// <summary>
/// 施法动画帧同步接口 — 技能效果实现此接口后，
/// EnemySkillManager 施放时自动以"效果出现帧"作为技能延迟（替代 _castDelay），
/// 特效出现/持续时间与施法动画帧严格对齐。
/// </summary>
public interface ICastAnimationSync
{
    /// <summary>施法动画帧率（fps）</summary>
    float CastAnimationFps { get; }

    /// <summary>特效出现帧（1-based，对应施法动画第 N 帧）</summary>
    int EffectStartFrame { get; }

    /// <summary>特效出现延迟（秒）= (EffectStartFrame - 1) / CastAnimationFps</summary>
    float EffectStartDelay { get; }
}
