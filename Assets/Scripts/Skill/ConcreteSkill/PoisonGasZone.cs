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

    // ── 运行时状态 ──
    private float _timer;
    private float _nextRefreshTime;
    private SpriteRenderer _sr;
    private Material _mat;
    private float _baseOpacity = 0.55f;
    private bool _initialized;

    /// <summary>初始化（外部调用覆盖 Inspector 参数）</summary>
    public void Initialize(Projectile.OwnerType owner, float radius, float duration, BuffData debuff)
    {
        _owner = owner;
        _radius = radius;
        _duration = duration;
        _debuffData = debuff;
        _initialized = true;
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

            buffMgr.ApplyBuff(_debuffData, null);
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
