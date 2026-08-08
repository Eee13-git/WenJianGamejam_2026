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

    /// <summary>是否正在喷射中</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 初始化腐蚀洪流。
    /// </summary>
    public void Initialize(ISkillCaster caster, Vector2 direction,
                           float duration, float range, float spreadAngleDeg,
                           float damagePerSecond, float damageInterval,
                           float knockbackSpeed,
                           Material torrentMaterial)
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

        _timer -= Time.deltaTime;

        // 玩家阵营：方向实时跟随鼠标；敌人阵营保持朝向玩家
        UpdateDirection();

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
        if (_ownerType == Projectile.OwnerType.Player && Camera.main != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector2 toMouse = (Vector2)(mouseWorld - _casterGO.transform.position);
            if (toMouse.sqrMagnitude > 0.0001f)
                _castDirection = toMouse.normalized;
        }
        else if (_ownerType == Projectile.OwnerType.Enemy)
        {
            // 敌人阵营：朝向目标方向（GetTargetDirection 已实现朝向玩家）
            Vector2 targetDir = _caster.GetTargetDirection();
            if (targetDir.sqrMagnitude > 0.0001f)
                _castDirection = targetDir.normalized;
        }
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
        Vector2 origin = _casterGO.transform.position;

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
        if (_torrentMaterial != null)
        {
            _visualMatInstance = new Material(_torrentMaterial);
            _visualRenderer.material = _visualMatInstance;
        }
        _visualRenderer.sprite = CreateWhiteSprite();
        _visualRenderer.sortingOrder = 50;

        float scale = _range * 2f;
        _visualGO.transform.localScale = new Vector3(scale, scale, 1f);

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

        // 位置跟随施法者
        _visualGO.transform.position = _casterGO.transform.position;

        // 旋转朝向喷射方向
        float angle = Mathf.Atan2(_castDirection.y, _castDirection.x) * Mathf.Rad2Deg;
        _visualGO.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 粒子系统跟随并旋转（Cone 沿局部 +Z 发射，用 LookRotation 映射到 XY 平面）
        if (_sprayParticles != null)
        {
            var psT = _sprayParticles.transform;
            psT.position = _casterGO.transform.position;
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
