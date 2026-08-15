using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 三叉喷射运行时组件 — 沿三个固定方向（Y 字形三个角）同时持续喷射细洪流。
/// 期间施法者无法移动（玩家锁 InputLocked / 敌人禁 EnemyMovement），
/// 三束方向固定：上(90°)/左下(210°)/右下(330°)，不随玩家位置变化。
/// 伤害按每束扇形独立判定，同一目标多束命中只结算一次（HashSet 去重）。
/// </summary>
public class TriSprayRuntime : MonoBehaviour
{
    // ── 配置 ──
    private ISkillCaster _caster;
    private float _duration;
    private float _range;
    private float _spreadAngleDeg;
    private float _damagePerSecond;
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
    private readonly List<SpriteRenderer> _visualRenderers = new List<SpriteRenderer>();
    private readonly List<Material> _visualMats = new List<Material>();
    private readonly List<ParticleSystem> _sprayParticles = new List<ParticleSystem>();

    /// <summary>三个固定方向角度（Y 字形三叶：上/左下/右下，互成 120°）</summary>
    private static readonly float[] Angles = { 90f, 210f, 330f };

    public bool IsActive => _isActive;

    public void Initialize(ISkillCaster caster, float duration, float range, float spreadAngleDeg,
                           float damagePerSecond, float damageInterval, Material torrentMaterial)
    {
        _caster = caster;
        _duration = duration;
        _range = range;
        _spreadAngleDeg = spreadAngleDeg;
        _damagePerSecond = damagePerSecond;
        _damageInterval = Mathf.Max(damageInterval, 0.05f);
        _torrentMaterial = torrentMaterial;
        _ownerType = caster.GetOwnerType();

        _timer = duration;
        _isActive = true;
        _nextDamageTime = 0f;

        _casterGO = caster.CasterTransform.gameObject;
        _casterRb = _casterGO.GetComponent<Rigidbody2D>();
        _playerController = _casterGO.GetComponent<PlayerController>();
        _enemyMovement = _casterGO.GetComponent<EnemyMovement>();

        // 锁定移动
        if (_playerController != null)
            _playerController.InputLocked = true;
        if (_enemyMovement != null)
            _enemyMovement.enabled = false;
        if (_casterRb != null)
            _casterRb.velocity = Vector2.zero;

        // 三束视觉
        foreach (var angle in Angles)
            CreateVisual(angle);
    }

    private void Update()
    {
        if (!_isActive) return;

        // 施法者死亡/被销毁 → 立即结束并销毁自身（连同视觉与粒子子对象）
        if (_casterGO == null)
        {
            EndEffect();
            return;
        }

        _timer -= Time.deltaTime;

        if (Time.time >= _nextDamageTime)
        {
            _nextDamageTime = Time.time + _damageInterval;
            ApplyDamage();
        }

        UpdateVisual();

        if (_timer <= 0f)
            EndEffect();
    }

    // ── 伤害：三束扇形独立判定 + 去重 ──
    private void ApplyDamage()
    {
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        float halfAngle = _spreadAngleDeg * 0.5f;
        float tickDamage = _damagePerSecond * _damageInterval;
        Vector2 origin = _casterGO.transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, _range);
        var damaged = new HashSet<Collider2D>();

        foreach (var angle in Angles)
        {
            Vector2 dir = AngleToDir(angle);

            foreach (var hit in hits)
            {
                if (hit == null || damaged.Contains(hit)) continue;
                if (!hit.CompareTag(targetTag)) continue;

                Vector2 toTarget = (Vector2)hit.transform.position - origin;
                float dist = toTarget.magnitude;
                if (dist > _range || dist < 0.01f) continue;

                // 扇形角度过滤
                if (Vector2.Angle(toTarget, dir) > halfAngle) continue;

                damaged.Add(hit);
                if (hit.TryGetComponent<IDamageable>(out var d))
                    d.TakeDamage(tickDamage);
            }
        }
    }

    // ── 视觉 ──
    private void CreateVisual(float angle)
    {
        var go = new GameObject("TriSprayVisual");
        go.transform.SetParent(transform);

        var sr = go.AddComponent<SpriteRenderer>();
        if (_torrentMaterial != null)
        {
            var mat = new Material(_torrentMaterial);
            sr.material = mat;
            _visualMats.Add(mat);
        }
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 50;

        float scale = _range * 2f;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.position = _casterGO.transform.position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _visualRenderers.Add(sr);

        // 液滴粒子
        var ps = CreateSprayParticles(angle);
        _sprayParticles.Add(ps);
    }

    private ParticleSystem CreateSprayParticles(float angle)
    {
        var psGO = new GameObject("TriSprayParticles");
        psGO.transform.SetParent(transform);
        psGO.transform.position = _casterGO.transform.position;

        var ps = psGO.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.8f, 0.35f, 0.9f),
            new Color(0.02f, 0.35f, 0.15f, 0.7f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 90f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = _spreadAngleDeg * 0.5f;
        shape.radius = 0.1f;
        shape.length = 0.5f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.4f, 1.0f, 0.5f), 0f), new GradientColorKey(new Color(0.1f, 0.6f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 0.85f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = psGO.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial();
        renderer.sortingOrder = 60;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // 朝向：Cone 沿局部 +Z，映射到 XY 平面
        var dir = AngleToDir(angle);
        psGO.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, dir.y, 0f), Vector3.forward);

        return ps;
    }

    private void UpdateVisual()
    {
        // 施法者被击退/弹开时，视觉与粒子都跟随其当前位置（旋转保持固定）
        Vector3 casterPos = _casterGO.transform.position;

        foreach (var sr in _visualRenderers)
        {
            if (sr != null)
                sr.transform.position = casterPos;
        }

        foreach (var ps in _sprayParticles)
        {
            if (ps != null)
                ps.transform.position = casterPos;
        }
    }

    private static Vector2 AngleToDir(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static Material CreateParticleMaterial()
    {
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

    private void EndEffect()
    {
        if (!_isActive) return;
        _isActive = false;

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
        if (_isActive)
        {
            if (_playerController != null)
                _playerController.InputLocked = false;
            if (_enemyMovement != null)
                _enemyMovement.enabled = true;
            if (_casterRb != null)
                _casterRb.velocity = Vector2.zero;
        }

        // 手动清理视觉与粒子子对象（防御性，正常 Destroy(gameObject) 已随父对象销毁）
        foreach (var sr in _visualRenderers)
            if (sr != null) Destroy(sr.gameObject);
        foreach (var ps in _sprayParticles)
            if (ps != null) Destroy(ps.gameObject);
        _visualRenderers.Clear();
        _sprayParticles.Clear();
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
