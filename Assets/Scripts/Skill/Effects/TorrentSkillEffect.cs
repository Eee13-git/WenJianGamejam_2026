using UnityEngine;

/// <summary>
/// 腐蚀性洪流技能效果：向前方呈扇形持续喷射腐蚀性洪流。
/// 期间施法者无法移动，仅可被喷射后坐力推动（朝向与喷射方向相反）。
/// 参考《以撒的结合》撒旦头/硫磺火的持续喷射形态。
/// 泛用类：持续时间、射程、扇形角度、每秒伤害、后坐力均可配置。
/// 右键 -> Create -> Game -> Skill Effect -> Corrosive Torrent
/// </summary>
[CreateAssetMenu(fileName = "TorrentEffect", menuName = "Game/Skill Effect/Corrosive Torrent")]
public class TorrentSkillEffect : SkillEffectBase
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

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        // 已有同类效果在进行中则不重复触发
        var existing = FindObjectOfType<TorrentRuntime>();
        if (existing != null && existing.IsActive) return;

        var go = new GameObject("CorrosiveTorrent");
        var runtime = go.AddComponent<TorrentRuntime>();

        runtime.Initialize(
            caster, direction,
            _duration, _range, _spreadAngle,
            _damagePerSecond, _damageInterval,
            _knockbackSpeed,
            _torrentMaterial
        );
    }
}
