using UnityEngine;

/// <summary>
/// 地面溶液持续伤害区 — 挂在投射物预制体上，原地生成一滩溶液。
/// 给圈内目标（阵营由所属 Projectile 决定）挂持续伤害 debuff；
/// 目标在圈内停留会持续刷新 debuff 持续时间（BuffManager.ApplyBuff 的 Refresh 行为）。
/// duration 秒后区域销毁。由 ProjectileSkillEffect(speed=0) 原地释放生成。
/// </summary>
public class GroundSolutionZone : MonoBehaviour
{
    [Header("区域配置")]
    [Tooltip("作用半径")]
    [SerializeField] private float _radius = 2f;
    [Tooltip("持续时间（秒），到时区域销毁")]
    [SerializeField] private float _duration = 10f;
    [Tooltip("检测/刷新间隔（秒），越小 debuff 刷新越及时")]
    [SerializeField] private float _refreshInterval = 0.25f;
    [Tooltip("给目标挂的持续伤害 debuff（DamageOverTimeBuff，Refresh 行为）")]
    [SerializeField] private BuffData _debuffData;

    // ── 运行时状态 ──
    private Projectile.OwnerType _owner;
    private GameObject _casterGO;
    private float _timer;
    private float _nextRefreshTime;
    private bool _initialized;

    /// <summary>初始化（可选外部调用覆盖 Inspector 参数；不调用则用 Inspector 值）</summary>
    public void Initialize(float radius, float duration, BuffData debuffData)
    {
        _radius = radius;
        _duration = duration;
        _debuffData = debuffData;
        _initialized = true;
    }

    private void Awake()
    {
        // 溶液不移动、不触发命中：Awake 立即禁用 Projectile（早于物理检测，
        // 防止 OnTriggerEnter2D 在 Start 前触发导致溶液被命中销毁）。
        var proj = GetComponent<Projectile>();
        if (proj != null)
            proj.enabled = false;
    }

    private void Start()
    {
        var proj = GetComponent<Projectile>();
        if (proj != null)
        {
            // Initialize 安排了自动回收，这里取消（溶液自行管理生命周期）
            proj.CancelInvoke("ReturnToPool");
            _owner = proj.Owner;
            _casterGO = proj.Caster;
        }
        else
        {
            _owner = Projectile.OwnerType.Player;
            Debug.LogWarning("GroundSolutionZone: 未找到 Projectile 组件，默认阵营 Player");
        }

        // 修复：保存预制体时运行时创建的 Texture2D 引用会丢失 → SpriteRenderer.sprite 为 null 不渲染。
        // 若 sprite 缺失则动态生成纯白 1x1（shader 用 UV 计算圆形，不需纹理内容）。
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
            sr.sprite = CreateWhiteSprite();

        // 未外部调用 Initialize 时使用 Inspector 参数（ProjectileSkillEffect 走此路径）
        _initialized = true;
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

            // 目标需要有 BuffManager 才能挂 buff
            var buffMgr = hit.GetComponent<BuffManager>();
            if (buffMgr == null) continue;

            buffMgr.ApplyBuff(_debuffData, _casterGO);
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
