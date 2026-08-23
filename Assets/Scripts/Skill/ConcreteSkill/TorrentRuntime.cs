using UnityEngine;

/// <summary>
/// 腐蚀性洪流运行时组件：向前方扇形持续喷射腐蚀洪流，对范围内目标持续造成伤害。
/// 期间施法者无法移动（玩家锁 InputLocked / 敌人禁 EnemyMovement），
/// 仅可被喷射后坐力推动（朝向与喷射方向相反）。
/// 玩家阵营时喷射方向实时跟随鼠标；敌人阵营时朝向玩家。
/// </summary>
public class TorrentRuntime : MonoBehaviour
{
    // ── 配置（由 Initialize 设置）──
    private ISkillCaster _caster;
    private Vector2 _castDirection;
    private float _duration;
    private float _range;
    private float _spreadAngleDeg;
    private float _damagePerSecond;
    private float _knockbackSpeed;
    private float _damageInterval;
    private Material _torrentMaterial;
    private Projectile.OwnerType _ownerType;

    // ── 施法者引用 ──
    private GameObject _casterGO;
    private Rigidbody2D _casterRb;
    private PlayerController _playerController;
    private EnemyMovement _enemyMovement;

    // ── 运行时状态 ──
    private float _timer;
    private bool _isActive;
    private float _nextDamageTime;

    // ── 视觉 ──
    private GameObject _visualGO;
    private SpriteRenderer _visualRenderer;
    private Material _visualMatInstance;
    private ParticleSystem _sprayParticles;

    // ── 帧动画视觉（可选：提供 Sprite 序列后用帧动画替代 shader 扇形视觉）──
    private Sprite[] _animationSprites;
    private float _animationFps;
    private float _animTimer;

    // ── 方向锁定 / 喷射原点 ──
    private bool _lockHorizontal;
    private float _originOffset;

    /// <summary>是否正在喷射中</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 初始化腐蚀洪流。
    /// 若传入 animationSprites（非空），使用帧动画精灵序列作为洪流视觉（替代 shader 扇形），
    /// 否则使用 shader 视觉（向后兼容）。
    /// lockHorizontal=true 时喷射方向锁定为水平（左右），忽略垂直分量；
    /// originOffset>0 时喷射原点（视觉+伤害判定）沿喷射方向前移，特效在施法者身前生成。
    /// </summary>
    public void Initialize(ISkillCaster caster, Vector2 direction,
                           float duration, float range, float spreadAngleDeg,
                           float damagePerSecond, float damageInterval,
                           float knockbackSpeed,
                           Material torrentMaterial,
                           Sprite[] animationSprites = null, float animationFps = 8f,
                           bool lockHorizontal = false, float originOffset = 0f)
    {
        _caster = caster;
        _castDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _duration = duration;
        _range = range;
        _spreadAngleDeg = spreadAngleDeg;
        _damagePerSecond = damagePerSecond;
        _damageInterval = Mathf.Max(damageInterval, 0.05f);
        _knockbackSpeed = knockbackSpeed;
        _torrentMaterial = torrentMaterial;
        _animationSprites = animationSprites;
        _animationFps = Mathf.Max(animationFps, 1f);
        _animTimer = 0f;
        _lockHorizontal = lockHorizontal;
        _originOffset = originOffset;
        _ownerType = caster.GetOwnerType();

        _timer = duration;
        _isActive = true;
        _nextDamageTime = 0f;

        _casterGO = caster.CasterTransform.gameObject;
        _casterRb = _casterGO.GetComponent<Rigidbody2D>();
        _playerController = _casterGO.GetComponent<PlayerController>();
        _enemyMovement = _casterGO.GetComponent<EnemyMovement>();

        // 锁定施法者移动：玩家锁 InputLocked（WASD+普攻失效），敌人禁 EnemyMovement
        if (_playerController != null)
            _playerController.InputLocked = true;
        if (_enemyMovement != null)
            _enemyMovement.enabled = false;
        if (_casterRb != null)
            _casterRb.velocity = Vector2.zero;

        CreateVisual();
    }

    // ──────────────────────────────────────────────
    //  每帧更新
    // ──────────────────────────────────────────────

    private void Update()
    {
        if (!_isActive) return;

        // 施法者被销毁（Boss 死亡等）→ 立即结束特效，避免残留+空引用异常
        if (_caster == null || _casterGO == null)
        {
            EndEffect();
            return;
        }

        _timer -= Time.deltaTime;

        // 玩家阵营：方向实时跟随鼠标；敌人阵营保持朝向玩家
        UpdateDirection();

        // 帧动画推进
        if (_animationSprites != null && _animationSprites.Length > 0)
        {
            _animTimer += Time.deltaTime;
            int frame = Mathf.FloorToInt(_animTimer * _animationFps) % _animationSprites.Length;
            if (_visualRenderer != null && _visualRenderer.sprite != _animationSprites[frame])
                _visualRenderer.sprite = _animationSprites[frame];
        }

        // 持续伤害（按 tick 间隔结算）
        if (Time.time >= _nextDamageTime)
        {
            _nextDamageTime = Time.time + _damageInterval;
            ApplyDamage();
        }

        // 后坐力推动（持续施加，撞墙被碰撞系统阻挡）
        ApplyKnockback();

        // 视觉跟随 + 旋转
        UpdateVisual();

        if (_timer <= 0f)
            EndEffect();
    }

    // ──────────────────────────────────────────────
    //  方向
    // ──────────────────────────────────────────────

    private void UpdateDirection()
    {
        // 玩家阵营：全方向实时跟随鼠标（不受水平锁定限制）
        if (_ownerType == Projectile.OwnerType.Player && Camera.main != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector2 toMouse = (Vector2)(mouseWorld - _casterGO.transform.position);
            if (toMouse.sqrMagnitude > 0.0001f)
                _castDirection = toMouse.normalized;
            return;
        }

        // 敌人阵营：水平锁定 → 只取目标方向的水平分量（左右），垂直分量忽略
        if (_lockHorizontal)
        {
            Vector2 targetDir = _caster.GetTargetDirection();
            // 玩家几乎在正上方/正下方时（x≈0），沿用当前水平朝向避免抖动
            float sx = Mathf.Abs(targetDir.x) < 0.05f ? Mathf.Sign(_castDirection.x) : Mathf.Sign(targetDir.x);
            if (Mathf.Abs(sx) < 0.01f) sx = 1f;
            _castDirection = new Vector2(sx, 0f);
            return;
        }

        // 敌人阵营：默认朝向目标方向（GetTargetDirection 已实现朝向玩家）
        Vector2 targetDir2 = _caster.GetTargetDirection();
        if (targetDir2.sqrMagnitude > 0.0001f)
            _castDirection = targetDir2.normalized;
    }

    // ──────────────────────────────────────────────
    //  伤害
    // ──────────────────────────────────────────────

    private void ApplyDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(_casterGO.transform.position, _range);
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        float tickDamage = _damagePerSecond * _damageInterval;
        float halfAngle = _spreadAngleDeg * 0.5f;
        // 伤害判定原点 = 施法者位置 + 喷射方向偏移（特效在身前生成时伤害也同步前移）
        Vector2 origin = (Vector2)_casterGO.transform.position + _castDirection * _originOffset;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (!hit.CompareTag(targetTag)) continue;

            Vector2 toTarget = (Vector2)hit.transform.position - origin;
            float dist = toTarget.magnitude;
            if (dist > _range || dist < 0.01f) continue;

            // 扇形角度过滤
            if (Vector2.Angle(toTarget, _castDirection) > halfAngle) continue;

            if (hit.TryGetComponent<IDamageable>(out var d))
                d.TakeDamage(tickDamage);
        }
    }

    // ──────────────────────────────────────────────
    //  后坐力
    // ──────────────────────────────────────────────

    private void ApplyKnockback()
    {
        if (_casterRb == null) return;

        // 后坐力方向 = 喷射方向的反方向（远离鼠标）
        Vector2 knockDir = -_castDirection;
        _casterRb.velocity = knockDir * _knockbackSpeed;
    }

    // ──────────────────────────────────────────────
    //  视觉
    // ──────────────────────────────────────────────

    private void CreateVisual()
    {
        _visualGO = new GameObject("TorrentVisual");
        _visualGO.transform.SetParent(transform);

        _visualRenderer = _visualGO.AddComponent<SpriteRenderer>();

        if (_animationSprites != null && _animationSprites.Length > 0)
        {
            // 帧动画视觉：sprite 左缘锚定在施法者（喷嘴），沿喷射方向延伸
            _visualRenderer.sprite = _animationSprites[0];
            _visualRenderer.sortingOrder = 50;
            _visualGO.transform.localScale = Vector3.one;
        }
        else
        {
            // shader 扇形视觉（向后兼容）
            if (_torrentMaterial != null)
            {
                _visualMatInstance = new Material(_torrentMaterial);
                _visualRenderer.material = _visualMatInstance;
            }
            _visualRenderer.sprite = CreateWhiteSprite();
            _visualRenderer.sortingOrder = 50;

            float scale = _range * 2f;
            _visualGO.transform.localScale = new Vector3(scale, scale, 1f);
        }

        UpdateVisual();

        // 液滴粒子：真正的喷溅液体感
        _sprayParticles = CreateSprayParticles();
    }

    /// <summary>
    /// 创建液滴喷射粒子系统（模拟撒旦头式液体喷溅）。
    /// </summary>
    private ParticleSystem CreateSprayParticles()
    {
        var psGO = new GameObject("TorrentSpray");
        psGO.transform.SetParent(transform);
        psGO.transform.position = _casterGO.transform.position;

        var ps = psGO.AddComponent<ParticleSystem>();

        // 主模块（不设 duration，技能结束销毁；loop 默认无限）
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.8f, 0.35f, 0.9f),
            new Color(0.02f, 0.35f, 0.15f, 0.7f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.playOnAwake = true;

        // 发射速率
        var emission = ps.emission;
        emission.rateOverTime = 180f;

        // Shape：锥形（沿 +Z 前方喷射）
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = _spreadAngleDeg * 0.5f;
        shape.radius = 0.1f;
        shape.length = 0.5f;

        // 寿命内大小衰减（液滴消散）
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        // 颜色随寿命淡出
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.4f, 1.0f, 0.5f), 0f), new GradientColorKey(new Color(0.1f, 0.6f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 0.85f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = colorGradient;

        // 渲染：使用白色圆形纹理 + 可调色材质
        var renderer = psGO.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial();
        renderer.sortingOrder = 60;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        return ps;
    }

    /// <summary>创建液滴粒子材质（白色圆形纹理 + 柔边）</summary>
    private static Material CreateParticleMaterial()
    {
        // 生成 32x32 径向柔边圆纹理
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x + 0.5f) / 32f - 0.5f;
                float dy = (y + 0.5f) / 32f - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.SmoothStep(0f, 1f, a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        return mat;
    }

    private void UpdateVisual()
    {
        if (_visualGO == null) return;

        // 视觉锚点 = 施法者位置 + 喷射方向偏移（特效在身前生成）
        Vector2 anchor = (Vector2)_casterGO.transform.position + _castDirection * _originOffset;

        // 位置跟随施法者
        _visualGO.transform.position = anchor;

        // 旋转朝向喷射方向
        float angle = Mathf.Atan2(_castDirection.y, _castDirection.x) * Mathf.Rad2Deg;
        _visualGO.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 粒子系统跟随并旋转（Cone 沿局部 +Z 发射，用 LookRotation 映射到 XY 平面）
        if (_sprayParticles != null)
        {
            var psT = _sprayParticles.transform;
            psT.position = anchor;
            // 局部 +Z → 世界 (dir.x, dir.y, 0)，使粒子沿喷射方向在 XY 平面飞行
            psT.rotation = Quaternion.LookRotation(
                new Vector3(_castDirection.x, _castDirection.y, 0f), Vector3.forward);
            psT.localScale = Vector3.one;
        }

        // shader 参数
        if (_visualMatInstance != null)
        {
            _visualMatInstance.SetFloat("_Range", _range);
            _visualMatInstance.SetFloat("_SpreadAngle", _spreadAngleDeg * Mathf.Deg2Rad);
        }
    }

    // ──────────────────────────────────────────────
    //  结束 / 清理
    // ──────────────────────────────────────────────

    private void EndEffect()
    {
        if (!_isActive) return;
        _isActive = false;

        // 解锁移动（玩家 + 敌人）
        if (_playerController != null)
            _playerController.InputLocked = false;
        if (_enemyMovement != null)
            _enemyMovement.enabled = true;
        if (_casterRb != null)
            _casterRb.velocity = Vector2.zero;

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 确保解锁（防止意外销毁时施法者卡死）
        if (_isActive)
        {
            if (_playerController != null)
                _playerController.InputLocked = false;
            if (_enemyMovement != null)
                _enemyMovement.enabled = true;
            if (_casterRb != null)
                _casterRb.velocity = Vector2.zero;
        }
    }

    /// <summary>生成纯白 1x1 sprite（PPU=1），scale 直接等于世界尺寸</summary>
    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
