using UnityEngine;

/// <summary>
/// 毒气团持续伤害区 — 挂在毒气预制体上，原地生成一团有毒气体。
/// 给圈内目标（阵营由 Owner 决定）挂持续伤害 debuff（DamageOverTimeBuff，Refresh 行为）。
/// poisonDuration 秒后区域淡出并销毁。独立组件，不依赖 Projectile。
/// </summary>
public class PoisonGasZone : MonoBehaviour
{
    [Header("区域配置")]
    [SerializeField] private float _radius = 2f;
    [SerializeField] private float _duration = 3f;
    [SerializeField] private float _refreshInterval = 0.25f;
    [SerializeField] private BuffData _debuffData;
    [Tooltip("毒气阵营：Enemy=伤害玩家，Player=伤害敌人")]
    [SerializeField] private Projectile.OwnerType _owner = Projectile.OwnerType.Enemy;
    [Tooltip("DOT 每 tick 伤害倍率（×施法者攻击力 × 技能倍率）")]
    [SerializeField] private float _damageMultiplier = 1f;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外作用半径（0=不成长）")]
    [SerializeField] private float _radiusPerLevel = 0f;
    [Tooltip("每级额外持续时间（秒，0=不成长）")]
    [SerializeField] private float _durationPerLevel = 0f;

    // ── 运行时状态 ──
    private float _timer;
    private float _nextRefreshTime;
    private SpriteRenderer _sr;
    private Material _mat;
    private float _baseOpacity = 0.55f;
    private bool _initialized;
    private float _attackStrength = 0f;
    private float _skillDamageMultiplier = 1f;
    private float _tickDamageOverride = -1f;

    /// <summary>初始化（外部调用覆盖 Inspector 参数）；attackStrength=施法者攻击力，damageMultiplier=技能倍率，level=技能等级</summary>
    public void Initialize(Projectile.OwnerType owner, float radius, float duration, BuffData debuff,
        float attackStrength = 0f, float damageMultiplier = 1f, int level = 1)
    {
        _owner = owner;
        _radius = radius;
        _duration = duration;
        _debuffData = debuff;
        _attackStrength = attackStrength;
        _skillDamageMultiplier = damageMultiplier;

        // 机制成长：作用半径/持续时间随技能等级提升
        ApplyLevel(level);

        // DOT 伤害 = 施法者攻击力 × 技能倍率 × 效果倍率（随攻击力成长）
        if (_damageMultiplier > 0f && _attackStrength > 0f)
            _tickDamageOverride = _attackStrength * _skillDamageMultiplier * _damageMultiplier;

        _initialized = true;
    }

    /// <summary>按技能等级成长作用半径/持续时间（level<=1 时无变化）</summary>
    public void ApplyLevel(int level)
    {
        if (level <= 1) return;
        if (_radiusPerLevel > 0f) _radius = _radius + (level - 1) * _radiusPerLevel;
        if (_durationPerLevel > 0f) _duration = _duration + (level - 1) * _durationPerLevel;
    }

    private void Start()
    {
        // 修复：保存预制体时运行时创建的 Texture2D 引用会丢失 → sprite 为 null 不渲染
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && _sr.sprite == null)
            _sr.sprite = CreateWhiteSprite();

        if (_sr != null)
        {
            _mat = _sr.material;
            if (_mat != null)
                _baseOpacity = _mat.GetFloat("_Opacity");
        }

        // 世界尺寸 = 半径 × 2（白 sprite PPU=1）
        transform.localScale = new Vector3(_radius * 2f, _radius * 2f, 1f);

        _timer = _duration;
        _nextRefreshTime = Time.time + _refreshInterval;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        _timer -= Time.deltaTime;

        // 淡出
        if (_mat != null)
        {
            float fade = Mathf.Clamp01(_timer / _duration);
            _mat.SetFloat("_Opacity", _baseOpacity * fade);
        }

        if (_timer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + _refreshInterval;
            RefreshDebuffs();
        }
    }

    /// <summary>给圈内目标应用/刷新 debuff</summary>
    private void RefreshDebuffs()
    {
        if (_debuffData == null) return;

        string targetTag = _owner == Projectile.OwnerType.Player ? "Enemy" : "Player";

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _radius);
        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;

            var buffMgr = hit.GetComponent<BuffManager>();
            if (buffMgr == null) continue;

            var buff = buffMgr.ApplyBuff(_debuffData, null);
            // 按施法者攻击力覆盖 DOT 每 tick 伤害
            if (buff != null && _tickDamageOverride > 0f)
                buff.TickDamageOverride = _tickDamageOverride;
        }
    }

    /// <summary>生成纯白 1x1 sprite（PPU=1），供无贴图 shader 使用</summary>
    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
