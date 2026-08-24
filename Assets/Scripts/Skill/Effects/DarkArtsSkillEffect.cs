using UnityEngine;

/// <summary>
/// 暗仪刺刀技能效果（重做版，参考《以撒的结合》Dark Arts）：
/// 1) 施法者进入潜行：半透明 + 无敌
/// 2) 连锁追击：瞬移到最近的敌人或敌方投射物，命中后追击下一个最近目标，最多 7 跳
/// 3) 到达终点后：沿整条路径生成闪电链连接（复用 ChainLightningArcRuntime 泛用视觉）并结算伤害
/// 泛用参数驱动，可被任意"潜行+连锁瞬移"类技能复用。
/// 右键 -> Create -> Game -> Skill Effect -> Dark Arts
/// </summary>
[CreateAssetMenu(fileName = "DarkArtsEffect", menuName = "Game/Skill Effect/Dark Arts")]
public class DarkArtsSkillEffect : SkillEffectBase
{
    [Header("连锁配置")]
    [Tooltip("最大追击次数（含首次命中，最多 7）")]
    public int maxChains = 7;
    [Tooltip("目标搜索半径")]
    public float searchRadius = 10f;
    [Tooltip("每跳间隔（秒），越小越迅捷")]
    public float jumpDelay = 0.12f;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外追击次数（0=不成长）")]
    public int maxChainsPerLevel = 0;
    [Tooltip("每级额外搜索半径（0=不成长）")]
    public float searchRadiusPerLevel = 0f;

    [Header("潜行")]
    [Tooltip("潜行时精灵透明度（0~1，0.35=半透明）")]
    public float stealthAlpha = 0.35f;

    [Header("闪电链视觉")]
    [Tooltip("闪电弧材质（加法混合发光，暗紫）")]
    public Material arcMaterial;
    [Tooltip("闪电弧锯齿段数")]
    public int arcSegments = 8;
    [Tooltip("闪电弧抖动幅度")]
    public float arcJitter = 0.35f;
    [Tooltip("闪电弧宽度")]
    public float arcWidth = 0.14f;
    [Tooltip("闪电弧持续时间")]
    public float arcDuration = 0.3f;
    [Tooltip("闪电弧颜色（暗紫核心）")]
    public Color arcColor = new Color(0.65f, 0.2f, 1f, 1f);

    [Header("命中辉光")]
    [Tooltip("命中辉光贴图（可选）")]
    public Sprite hitGlowSprite;
    [Tooltip("命中辉光颜色")]
    public Color hitGlowColor = new Color(0.8f, 0.4f, 1f, 1f);
    [Tooltip("命中辉光大小")]
    public float hitGlowSize = 1.2f;
    [Tooltip("命中辉光持续时间")]
    public float hitGlowDuration = 0.22f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
    {
        if (caster == null || caster.CasterTransform == null) return;

        var go = new GameObject("DarkArtsRuntime");
        var runtime = go.AddComponent<DarkArtsRuntime>();

        // 机制成长：追击次数/搜索半径随等级提升
        int actualMaxChains = Mathf.Max(1, maxChains + (level - 1) * maxChainsPerLevel);
        float actualSearchRadius = searchRadius + (level - 1) * searchRadiusPerLevel;

        runtime.Initialize(
            caster,
            actualMaxChains, actualSearchRadius, jumpDelay,
            stealthAlpha,
            caster.GetAttackStrength() * damageMultiplier,
            arcMaterial, arcSegments, arcJitter, arcWidth, arcDuration, arcColor,
            hitGlowSprite, hitGlowColor, hitGlowSize, hitGlowDuration
        );
    }
}
