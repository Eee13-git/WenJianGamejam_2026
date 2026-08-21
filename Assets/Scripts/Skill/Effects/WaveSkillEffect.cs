using UnityEngine;

/// <summary>
/// 泛用扩散波技能效果：从施法者处释放一道波扩散出去。
/// 支持两种形态：
///   - 全向圆形波：spreadAngle=360，波从中心向四周扩散
///   - 单向扇形波：spreadAngle&lt;360，沿施法方向发射一道弧形波
/// 波纹触及的敌人/子弹可根据配置造成伤害、冻结、画面扭曲等效果。
/// 所有效果均可选：damage=0 不造伤害；freezeDuration=0 不冻结；overlayMaterial=null 无覆盖层。
/// 右键 -> Create -> Game -> Skill Effect -> Diffusion Wave
/// </summary>
[CreateAssetMenu(fileName = "WaveEffect", menuName = "Game/Skill Effect/Diffusion Wave")]
public class WaveSkillEffect : SkillEffectBase
{
    [Header("波纹配置")]
    [Tooltip("波纹扩散速度（单位/秒）")]
    [SerializeField] private float _waveSpeed = 25f;
    [Tooltip("波纹最大半径")]
    [SerializeField] private float _maxRadius = 10f;
    [Tooltip("扩散角度（度）：360=全向圆形波；<360=单向扇形波（沿施法方向）")]
    [SerializeField] private float _spreadAngle = 360f;

    [Header("伤害（可选）")]
    [Tooltip("波纹命中伤害，0=不造成伤害")]
    [SerializeField] private float _damage = 0f;

    [Header("冻结（可选）")]
    [Tooltip("冻结持续时间（秒），0=不冻结")]
    [SerializeField] private float _freezeDuration = 2f;
    [Tooltip("是否冻结子弹")]
    [SerializeField] private bool _freezeProjectiles = true;

    [Header("击退（可选）")]
    [Tooltip("击退初速度（单位/秒），0=不击退。命中的目标沿径向从中心推开")]
    [SerializeField] private float _knockbackForce = 0f;
    [Tooltip("击退硬直时长（秒），期间目标被推开且暂停移动/AI")]
    [SerializeField] private float _knockbackDuration = 0.3f;

    [Header("取消机制（可选）")]
    [Tooltip("普攻时取消效果")]
    [SerializeField] private bool _cancelOnAttack = true;
    [Tooltip("释放其他技能时取消效果")]
    [SerializeField] private bool _cancelOnSkillCast = true;
    [Tooltip("取消监听宽限期（秒），技能释放后此时间内不响应取消")]
    [SerializeField] private float _cancelGracePeriod = 0.3f;

    [Header("视觉材质")]
    [Tooltip("波纹材质（null=无波纹视觉）")]
    [SerializeField] private Material _waveMaterial;
    [Tooltip("全屏覆盖材质（null=无覆盖层）")]
    [SerializeField] private Material _overlayMaterial;

    [Header("音效（可选）")]
    [Tooltip("波纹结束/消散时播放的音效名称（如 \"axon_block_end\"），空=不播放")]
    [SerializeField] private string _endSoundName;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        Vector2 origin = caster.CasterTransform.position;

        // 已有同类效果在进行中则不重复触发
        var existing = FindObjectOfType<WaveRuntime>();
        if (existing != null && existing.IsActive) return;

        var go = new GameObject("DiffusionWave");
        var runtime = go.AddComponent<WaveRuntime>();

        runtime.SetEndSound(_endSoundName);

        runtime.Initialize(
            origin, direction, _waveSpeed, _maxRadius, _spreadAngle,
            _damage * damageMultiplier,
            _freezeDuration, _freezeProjectiles,
            _cancelOnAttack, _cancelOnSkillCast, _cancelGracePeriod,
            _waveMaterial, _overlayMaterial,
            _knockbackForce, _knockbackDuration,
            ownerType
        );
    }
}
