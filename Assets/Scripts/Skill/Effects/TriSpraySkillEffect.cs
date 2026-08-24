using UnityEngine;

/// <summary>
/// 三叉喷射技能效果 — 沿三个固定方向（Y 字形三个角）同时喷射较细的持续洪流。
/// 结核分枝杆菌：形象为 Y 字形三叶结构，三个角固定指向 上(90°)/左下(210°)/右下(330°)。
/// 期间施法者无法移动，持续时间长。三束各自独立扇形，覆盖三叉方向。
/// 泛用类：持续时间、射程、扇形角度、每秒伤害均可配置。
/// 右键 -> Create -> Game -> Skill Effect -> Tri Spray
/// </summary>
[CreateAssetMenu(fileName = "TriSprayEffect", menuName = "Game/Skill Effect/Tri Spray")]
public class TriSpraySkillEffect : SkillEffectBase
{
    [Header("喷射配置")]
    [SerializeField] private float _duration = 8f;
    [SerializeField] private float _range = 8f;
    [Tooltip("单束扇形扩散角度（度），较细")]
    [SerializeField] private float _spreadAngle = 15f;
    [SerializeField] private float _damagePerSecondMultiplier = 1f;
    [SerializeField] private float _damageInterval = 0.2f;

    [Header("视觉材质")]
    [SerializeField] private Material _torrentMaterial;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外喷射持续时间（秒，0=不成长）")]
    public float durationPerLevel = 0f;
    [Tooltip("每级额外射程（0=不成长）")]
    public float rangePerLevel = 0f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
    {
        // 已有同类效果进行中则不重复触发
        var existing = FindObjectOfType<TriSprayRuntime>();
        if (existing != null && existing.IsActive) return;

        var go = new GameObject("TriSpray");
        var runtime = go.AddComponent<TriSprayRuntime>();

        // 机制成长：持续时长/射程随等级提升
        float actualDuration = _duration + (level - 1) * durationPerLevel;
        float actualRange = _range + (level - 1) * rangePerLevel;

        // 每秒伤害 = 施法者攻击力 × 等级倍率 × 效果倍率
        float dps = caster.GetAttackStrength() * damageMultiplier * _damagePerSecondMultiplier;

        runtime.Initialize(caster, actualDuration, actualRange, _spreadAngle, dps, _damageInterval, _torrentMaterial);
    }
}
