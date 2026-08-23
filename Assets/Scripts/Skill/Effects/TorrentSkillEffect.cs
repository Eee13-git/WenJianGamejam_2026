using UnityEngine;

/// <summary>
/// 腐蚀性洪流技能效果：向前方呈扇形持续喷射腐蚀性洪流。
/// 期间施法者无法移动，仅可被喷射后坐力推动（朝向与喷射方向相反）。
/// 参考《以撒的结合》撒旦头/硫磺火的持续喷射形态。
/// 泛用类：持续时间、射程、扇形角度、每秒伤害、后坐力均可配置。
/// 右键 -> Create -> Game -> Skill Effect -> Corrosive Torrent
/// </summary>
[CreateAssetMenu(fileName = "TorrentEffect", menuName = "Game/Skill Effect/Corrosive Torrent")]
public class TorrentSkillEffect : SkillEffectBase, ICastAnimationSync
{
    [Header("喷射配置")]
    [Tooltip("喷射持续时间（秒）")]
    [SerializeField] private float _duration = 20f;
    [Tooltip("喷射射程")]
    [SerializeField] private float _range = 8f;
    [Tooltip("扇形扩散角度（度）")]
    [SerializeField] private float _spreadAngle = 60f;
    [Tooltip("每秒伤害")]
    [SerializeField] private float _damagePerSecond = 20f;
    [Tooltip("伤害结算间隔（秒），越小越平滑")]
    [SerializeField] private float _damageInterval = 0.2f;

    [Header("后坐力")]
    [Tooltip("后坐力移动速度（朝向与喷射方向相反）")]
    [SerializeField] private float _knockbackSpeed = 2.5f;

    [Header("视觉材质")]
    [Tooltip("洪流材质（使用 CorrosiveTorrent shader）")]
    [SerializeField] private Material _torrentMaterial;

    [Header("帧动画视觉（可选）")]
    [Tooltip("帧动画精灵序列（提供后使用帧动画替代 shader 扇形视觉，如脓疮洪流）")]
    [SerializeField] private Sprite[] _animationSprites;
    [Tooltip("帧动画播放速率（帧/秒）")]
    [SerializeField] private float _animationFps = 8f;

    [Header("方向锁定与喷射原点")]
    [Tooltip("锁定水平方向（左右喷射，忽略垂直分量）。用于 Boss 水平型洪流")]
    [SerializeField] private bool _lockHorizontal;
    [Tooltip("喷射原点前移距离（世界单位），特效在施法者身前生成")]
    [SerializeField] private float _originOffset;

    [Header("施法动画帧同步（自动匹配）")]
    [Tooltip("施法动画帧率（fps），用于计算特效出现/消失时刻")]
    [SerializeField] private float _castAnimationFps = 8f;
    [Tooltip("特效出现帧（1-based，对应施法动画第 N 帧）")]
    [SerializeField] private int _effectStartFrame = 6;
    [Tooltip("特效完全消失帧（1-based）")]
    [SerializeField] private int _effectEndFrame = 8;
    [Tooltip("特效持续期间帧动画循环轮数（0=自动 1 轮）")]
    [SerializeField] private int _animationLoops = 1;

    /// <summary>施法动画帧率</summary>
    public float CastAnimationFps => _castAnimationFps;
    /// <summary>特效出现帧（1-based）</summary>
    public int EffectStartFrame => _effectStartFrame;
    /// <summary>特效出现延迟（秒）</summary>
    public float EffectStartDelay => (_effectStartFrame - 1) / Mathf.Max(_castAnimationFps, 1f);

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        // 已有同类效果在进行中则不重复触发
        var existing = FindObjectOfType<TorrentRuntime>();
        if (existing != null && existing.IsActive) return;

        var go = new GameObject("CorrosiveTorrent");
        var runtime = go.AddComponent<TorrentRuntime>();

        // 自动匹配：特效持续时长 = (消失帧 - 出现帧) / 动画帧率，帧动画 fps = 帧数×轮数 / 持续时长。
        // 未配置帧同步（结束帧<=开始帧）时回退手动 _duration（兼容溶栓灌注等旧资产）。
        float startTime = EffectStartDelay;
        float endTime = (_effectEndFrame - 1) / Mathf.Max(_castAnimationFps, 1f);
        float duration = _effectEndFrame > _effectStartFrame
            ? Mathf.Max(endTime - startTime, 0.1f)
            : _duration;
        float animFps = _animationSprites != null && _animationSprites.Length > 0
            ? _animationSprites.Length * Mathf.Max(_animationLoops, 1) / duration
            : _animationFps;

        runtime.Initialize(
            caster, direction,
            duration, _range, _spreadAngle,
            _damagePerSecond, _damageInterval,
            _knockbackSpeed,
            _torrentMaterial,
            _animationSprites, animFps,
            _lockHorizontal, _originOffset
        );
    }
}
