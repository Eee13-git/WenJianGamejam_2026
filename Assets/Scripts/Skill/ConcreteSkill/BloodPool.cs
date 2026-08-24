using UnityEngine;

/// <summary>
/// 血池 — 光柱落地后遗留的有机血迹区域。
/// 给进入圈内的敌人持续施加血色腐蚀 debuff（Refresh 行为），
/// duration 秒后消散。只影响敌方阵营。
/// </summary>
public class BloodPool : MonoBehaviour
{
    // ════════════════════════════════════════════
    //  血池视觉
    // ════════════════════════════════════════════
    [Header("──── 血池视觉 ────")]
    [Tooltip("血池材质（PhotonBloodPool），运行时可覆盖")]
    [SerializeField] private Material _poolMaterial;
    [Tooltip("血池呼吸脉动幅度（0=无脉动）")]
    [SerializeField] private float _breathAmplitude = 0.06f;
    [Tooltip("血池呼吸脉动速度")]
    [SerializeField] private float _breathSpeed = 4f;

    // ════════════════════════════════════════════
    //  血池机制
    // ════════════════════════════════════════════
    [Header("──── 血池机制 ────")]
    [Tooltip("作用半径")]
    [SerializeField] private float _radius = 1.5f;
    [Tooltip("持续时间（秒），到时消散")]
    [SerializeField] private float _duration = 10f;
    [Tooltip("debuff 刷新间隔（秒）")]
    [SerializeField] private float _refreshInterval = 0.25f;
    [Tooltip("给目标挂的血色腐蚀 debuff")]
    [SerializeField] private BuffData _debuffData;
    [Tooltip("DOT 每 tick 伤害倍率（×施法者攻击力 × 技能倍率）")]
    [SerializeField] private float _tickDamageMultiplier = 1f;

    [Header("──── 机制成长（升级可选）────")]
    [Tooltip("每级额外血池半径（0=不成长）")]
    [SerializeField] private float _radiusPerLevel = 0f;
    [Tooltip("每级额外持续时间（秒，0=不成长）")]
    [SerializeField] private float _durationPerLevel = 0f;

    // ── 运行时状态 ──
    private Projectile.OwnerType _ownerType;
    private GameObject _casterGO;
    private float _timer;
    private float _nextRefreshTime;
    private bool _initialized;
    private float _attackStrength = 0f;
    private float _skillDamageMultiplier = 1f;
    private float _tickDamageOverride = -1f;

    // ── 视觉引用 ──
    private SpriteRenderer _sr;
    private Material _matInstance;

    /// <summary>初始化血池（由 PhotonBeam.SpawnRadiancePool 调用）；level 供机制成长</summary>
    public void Initialize(Projectile.OwnerType ownerType, GameObject caster,
        float radius, float duration, int level = 1)
    {
        _ownerType = ownerType;
        _casterGO = caster;
        _radius = radius;
        _duration = duration;
        ApplyLevel(level);
        _initialized = true;
    }

    /// <summary>按技能等级成长血池半径/持续时间（level<=1 时无变化）</summary>
    public void ApplyLevel(int level)
    {
        if (level <= 1) return;
        if (_radiusPerLevel > 0f) _radius = _radius + (level - 1) * _radiusPerLevel;
        if (_durationPerLevel > 0f) _duration = _duration + (level - 1) * _durationPerLevel;
    }

    /// <summary>设置血色腐蚀 debuff</summary>
    public void SetDebuff(BuffData debuffData)
    {
        _debuffData = debuffData;
    }

    /// <summary>设置伤害参数：施法者攻击力 + 技能倍率，DOT = 攻击力 × 倍率 × 效果倍率</summary>
    public void SetDamageParams(float attackStrength, float damageMultiplier = 1f)
    {
        _attackStrength = attackStrength;
        _skillDamageMultiplier = damageMultiplier;
        if (_tickDamageMultiplier > 0f && _attackStrength > 0f)
            _tickDamageOverride = _attackStrength * _skillDamageMultiplier * _tickDamageMultiplier;
    }

    private void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
        {
            if (_poolMaterial != null)
                _sr.material = new Material(_poolMaterial);
            else if (_sr.sharedMaterial != null)
                _sr.material = new Material(_sr.sharedMaterial);
            _matInstance = _sr.material;
        }

        if (_sr != null && _sr.sprite == null)
            _sr.sprite = CreateWhiteSprite();

        transform.localScale = new Vector3(_radius * 2f, _radius * 2f, 1f);

        _timer = _duration;
        _nextRefreshTime = Time.time + _refreshInterval;
    }

    private void Update()
    {
        if (!_initialized) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (_matInstance != null && _breathAmplitude > 0f)
        {
            float breath = 1f + _breathAmplitude * Mathf.Sin(Time.time * _breathSpeed);
            transform.localScale = new Vector3(
                _radius * 2f * breath, _radius * 2f * breath, 1f);
        }

        if (Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + _refreshInterval;
            RefreshDebuffs();
        }
    }

    private void RefreshDebuffs()
    {
        if (_debuffData == null) return;

        string targetTag = _ownerType == Projectile.OwnerType.Player
            ? "Enemy" : "Player";

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, _radius, ~0);
        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;

            var enemy = hit.GetComponent<EnemyCore>();
            if (enemy != null && (enemy.IsDead || enemy.IsAssimilated))
                continue;

            var buffMgr = hit.GetComponent<BuffManager>();
            if (buffMgr == null) continue;

            var buff = buffMgr.ApplyBuff(_debuffData, _casterGO);
            // 按施法者攻击力覆盖 DOT 每 tick 伤害
            if (buff != null && _tickDamageOverride > 0f)
                buff.TickDamageOverride = _tickDamageOverride;
        }
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f), 1f);
    }
}
