using UnityEngine;

/// <summary>
/// 泛用滑移/冲刺技能效果 — 朝移动方向冲刺位移一段距离，期间无敌。
/// 玩家朝 WASD 移动方向（无输入则朝鼠标方向）；敌人随机方向。
/// 可选"卷起敌人"：位移途中卷起途经的敌人（吸附跟随 + 命中伤害），
/// 被卷起的敌人撞到墙/障碍后受到二次伤害并释放。
/// 可选"爆发冲击"：自动朝最近敌人突进，命中后周身释放气浪推开周边敌方。
/// 所有能力参数驱动：距离、速度、无敌时长、拖影材质、卷起/爆发开关均可配置。
/// 右键 -> Create -> Game -> Skill Effect -> Slide
/// </summary>
[CreateAssetMenu(fileName = "SlideEffect", menuName = "Game/Skill Effect/Slide")]
public class SlideSkillEffect : SkillEffectBase
{
    [Header("滑移配置")]
    [Tooltip("位移距离")]
    [SerializeField] private float _distance = 3f;
    [Tooltip("滑移速度（单位/秒）")]
    [SerializeField] private float _speed = 18f;
    [Tooltip("滑移期间无敌时长（秒）")]
    [SerializeField] private float _immuneDuration = 0.5f;
    [Tooltip("拖影材质（null=无拖影）")]
    [SerializeField] private Material _trailMaterial;

    [Header("卷起敌人（可选）")]
    [Tooltip("是否卷起途经的敌人")]
    [SerializeField] private bool _carryEnemies = false;
    [Tooltip("卷起检测半径")]
    [SerializeField] private float _carryRadius = 1f;
    [Tooltip("卷起命中伤害倍率（×施法者攻击力 × 等级倍率）")]
    [SerializeField] private float _carryDamageMultiplier = 1f;
    [Tooltip("卷起的敌人撞墙后的二次伤害倍率（×施法者攻击力 × 等级倍率）")]
    [SerializeField] private float _wallDamageMultiplier = 0f;
    [Tooltip("卷起的敌人是否无敌于其他伤害（跟随期间）")]
    [SerializeField] private bool _carriedInvincible = true;

    [Header("爆发冲击（可选）")]
    [Tooltip("自动朝最近的敌人方向突进（否则用移动/鼠标方向）")]
    [SerializeField] private bool _autoAimNearest = false;
    [Tooltip("命中敌人后周身释放气浪推开周边敌方（不卷走目标）")]
    [SerializeField] private bool _burstOnHit = false;
    [Tooltip("气浪半径")]
    [SerializeField] private float _burstRadius = 4f;
    [Tooltip("气浪推开力度（径向击退初速度）")]
    [SerializeField] private float _burstPushForce = 10f;
    [Tooltip("气浪视觉材质（可复用扩散波/气浪 shader 材质）")]
    [SerializeField] private Material _burstWaveMaterial;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外冲刺距离（0=不成长）")]
    public float distancePerLevel = 0f;
    [Tooltip("每级额外卷起检测半径（0=不成长）")]
    public float carryRadiusPerLevel = 0f;
    [Tooltip("每级额外气浪半径（0=不成长）")]
    public float burstRadiusPerLevel = 0f;

    [Header("音效（可选）")]
    [Tooltip("冲刺自然结束（到达终点/命中敌人）时播放的音效名称，空=不播放")]
    [SerializeField] private string _endSoundName;
    [Tooltip("冲刺撞墙结束时播放的音效名称（如 \"muscle_burst_wall\"），空=不播放")]
    [SerializeField] private string _wallSoundName;

    [Header("命中特效（可选）")]
    [Tooltip("命中敌人时的闪光材质（如 DarkDaggerTrail），null=无命中特效")]
    [SerializeField] private Material _hitFlashMaterial;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
    {
        // 已有滑移在进行中则不重复触发
        var existing = caster.CasterTransform.GetComponent<SlideRuntime>();
        if (existing != null && existing.IsActive) return;

        var slide = caster.CasterTransform.gameObject.AddComponent<SlideRuntime>();

        // 结束/撞墙音效配置
        slide.SetSounds(_endSoundName, _wallSoundName);

        // 敌人阵营：使用外部传入方向（Boss 轴向冲刺等）；玩家阵营由 SlideRuntime 内部读取输入
        if (ownerType == Projectile.OwnerType.Enemy && direction.sqrMagnitude > 0.01f)
            slide.SetForcedDirection(direction);

        // 机制成长：冲刺距离/卷起半径/气浪半径随等级提升
        float actualDistance = _distance + (level - 1) * distancePerLevel;
        float actualCarryRadius = _carryRadius + (level - 1) * carryRadiusPerLevel;
        float actualBurstRadius = _burstRadius + (level - 1) * burstRadiusPerLevel;

        slide.Activate(caster, actualDistance, _speed, _immuneDuration, _trailMaterial);

        // 卷起能力配置：伤害 = 施法者攻击力 × 等级倍率 × 效果倍率
        if (_carryEnemies)
        {
            float carryDmg = _carryDamageMultiplier > 0f
                ? caster.GetAttackStrength() * damageMultiplier * _carryDamageMultiplier
                : 0f;
            float wallDmg = _wallDamageMultiplier > 0f
                ? caster.GetAttackStrength() * damageMultiplier * _wallDamageMultiplier
                : 0f;

            slide.EnableCarrying(actualCarryRadius, carryDmg,
                wallDmg, _carriedInvincible, ownerType);
        }

        // 命中闪光特效（暗仪刺刀等强化）
        if (_hitFlashMaterial != null)
            slide.SetHitFlashMaterial(_hitFlashMaterial);

        // 爆发冲击配置（自动锁敌 + 命中释放气浪）
        if (_autoAimNearest || _burstOnHit)
        {
            slide.EnableBurst(_autoAimNearest, _burstOnHit, actualBurstRadius,
                _burstPushForce, _burstWaveMaterial, ownerType);
        }
    }
}
