using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 泛用连锁闪电技能效果 — 从施法者（或上一目标）位置链接最近的敌人，
/// 命中后闪电链再链接下一个最近的目标，最多链接 maxTargets-1 次。
/// 每次命中造成 施法者攻击力 × 伤害系数 的伤害。不使用投射物，纯瞬间连锁。
/// 所有视觉与数值均可通过资产配置，可被多个连锁类技能复用。
/// </summary>
[CreateAssetMenu(fileName = "ChainLightningEffect", menuName = "Game/Skill Effect/Chain Lightning")]
public class ChainLightningSkillEffect : SkillEffectBase
{
    [Header("连锁配置")]
    [Tooltip("最大命中目标数（含首个）")]
    public int maxTargets = 5;

    [Tooltip("初始目标搜索半径")]
    public float searchRadius = 12f;

    [Tooltip("每跳搜索半径")]
    public float jumpRadius = 10f;

    [Tooltip("每跳间隔秒数（越小越迅捷）")]
    public float jumpDelay = 0.06f;

    [Header("闪电弧视觉")]
    [Tooltip("闪电弧材质（加法混合发光）")]
    public Material arcMaterial;

    [Tooltip("闪电弧锯齿段数")]
    public int arcSegments = 8;

    [Tooltip("闪电弧抖动幅度")]
    public float arcJitter = 0.35f;

    [Tooltip("闪电弧宽度")]
    public float arcWidth = 0.12f;

    [Tooltip("闪电弧持续时间")]
    public float arcDuration = 0.22f;

    [Tooltip("闪电弧颜色（核心）")]
    public Color arcColor = new Color(0.6f, 0.95f, 1f, 1f);

    [Tooltip("闪电弧顶点刷新间隔（帧），越大锯齿跳动越少越稳")]
    public int arcRefreshInterval = 3;

    [Header("命中特效")]
    [Tooltip("命中辉光贴图（可选）")]
    public Sprite hitGlowSprite;

    [Tooltip("命中辉光颜色")]
    public Color hitGlowColor = new Color(0.7f, 1f, 1f, 1f);

    [Tooltip("命中辉光初始大小")]
    public float hitGlowSize = 1.2f;

    [Tooltip("命中辉光持续时间")]
    public float hitGlowDuration = 0.25f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        if (caster == null || caster.CasterTransform == null) return;

        MonoBehaviour runner = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (runner == null)
        {
            Debug.LogWarning("ChainLightningSkillEffect: caster has no MonoBehaviour to run coroutine!");
            return;
        }

        runner.StartCoroutine(ChainRoutine(caster, direction, damageMultiplier, ownerType));
    }

    private IEnumerator ChainRoutine(ISkillCaster caster, Vector2 direction,
        float damageMultiplier, Projectile.OwnerType ownerType)
    {
        float damage = caster.GetAttackStrength() * damageMultiplier;
        string targetTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";

        Vector3 origin = caster.CasterTransform.position;

        // 已命中的目标，避免重复
        HashSet<Collider2D> hitSet = new HashSet<Collider2D>();

        // 当前起点
        Transform current = FindNearestTarget(origin, targetTag, searchRadius, hitSet);
        if (current == null)
        {
            // 无目标时从施法者向施法方向空放一道闪电（视觉反馈）
            Vector3 end = origin + (Vector3)(direction.normalized * 2f);
            SpawnArc(origin, end);
            yield break;
        }

        Vector3 previousPos = origin;

        for (int i = 0; i < maxTargets; i++)
        {
            if (current == null) break;

            var col = current.GetComponent<Collider2D>();
            if (col != null)
            {
                hitSet.Add(col);
                if (current.TryGetComponent<IDamageable>(out var dmg))
                    dmg.TakeDamage(damage);
            }

            // 闪电弧：上一目标 -> 当前目标
            SpawnArc(previousPos, current.position);
            SpawnHitGlow(current.position);

            previousPos = current.position;

            // 链接下一个最近的目标（跳过已命中）
            if (i < maxTargets - 1)
            {
                yield return new WaitForSeconds(jumpDelay);
                current = FindNearestTarget(current.position, targetTag, jumpRadius, hitSet);
            }
        }
    }

    /// <summary>在范围内查找最近且未命中的目标</summary>
    private Transform FindNearestTarget(Vector3 origin, string targetTag,
        float radius, HashSet<Collider2D> hitSet)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;
            if (hitSet != null && hitSet.Contains(hit)) continue;

            // 跳过死亡目标
            if (hit.TryGetComponent<EnemyCore>(out var ec) && ec.IsDead) continue;

            float sqr = (hit.transform.position - origin).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = hit.transform;
            }
        }
        return best;
    }

    /// <summary>生成锯齿状闪电弧（每帧抖动，明亮迅捷）</summary>
    private void SpawnArc(Vector3 from, Vector3 to)
    {
        if (arcMaterial == null) return;
        float distance = Vector3.Distance(from, to);
        if (distance < 0.01f) return;

        var go = new GameObject("ChainLightningArc");
        go.transform.position = Vector3.zero;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = arcSegments + 1;
        lr.startWidth = arcWidth;
        lr.endWidth = arcWidth * 0.6f;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.sortingOrder = 25;
        lr.material = arcMaterial;

        // 核心抖动驱动：注册一个轻量更新器让弧线按间隔刷新锯齿
        var arc = go.AddComponent<ChainLightningArcRuntime>();
        arc.Init(lr, from, to, arcSegments, arcJitter, arcDuration, arcColor, arcRefreshInterval);

        Destroy(go, arcDuration);
    }

    /// <summary>命中位置生成辉光闪光</summary>
    private void SpawnHitGlow(Vector3 pos)
    {
        var go = new GameObject("ChainLightningHitGlow");
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 26;
        if (hitGlowSprite != null)
        {
            sr.sprite = hitGlowSprite;
            sr.color = hitGlowColor;
        }

        var glow = go.AddComponent<ChainLightningHitGlowRuntime>();
        glow.Init(sr, hitGlowSize, hitGlowDuration);

        Destroy(go, hitGlowDuration);
    }
}

/// <summary>
/// 闪电弧运行时组件 — 每帧重新生成锯齿顶点，实现明亮闪烁的闪电效果。
/// 颜色渐变：中间亮白、两端为边缘色（青蓝），整体随时间淡出。
/// 泛用视觉组件，可由任意需要锯齿电弧的效果类复用。
/// </summary>
public class ChainLightningArcRuntime : MonoBehaviour
{
    private LineRenderer _lr;
    private Vector3 _from;
    private Vector3 _to;
    private int _segments;
    private float _jitter;
    private float _duration;
    private Color _edgeColor;
    private float _elapsed;
    private int _refreshInterval = 1;
    private int _frameCounter;

    public void Init(LineRenderer lr, Vector3 from, Vector3 to,
        int segments, float jitter, float duration, Color edgeColor, int refreshInterval = 1)
    {
        _lr = lr;
        _from = from;
        _to = to;
        _segments = Mathf.Max(2, segments);
        _jitter = jitter;
        _duration = Mathf.Max(0.05f, duration);
        _edgeColor = edgeColor;
        _refreshInterval = Mathf.Max(1, refreshInterval);
        _frameCounter = 0;
        _elapsed = 0f;

        ApplyGradient(1f);
        RefreshPoints();
    }

    private void Update()
    {
        if (_lr == null) return;

        _elapsed += Time.deltaTime;
        float t = _elapsed / _duration;
        t = Mathf.Clamp01(t);

        // 整体 alpha 快速衰减
        float a = Mathf.Lerp(1f, 0f, t * t);
        ApplyGradient(a);

        // 按间隔刷新锯齿顶点（减少每帧跳动，更稳定）
        _frameCounter++;
        if (t < 1f && _frameCounter >= _refreshInterval)
        {
            _frameCounter = 0;
            RefreshPoints();
        }
    }

    /// <summary>应用颜色渐变：中间亮白、两端边缘色（青蓝），alpha 整体衰减</summary>
    private void ApplyGradient(float a)
    {
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(_edgeColor, 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(_edgeColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(a, 0f),
                new GradientAlphaKey(a, 0.5f),
                new GradientAlphaKey(a * 0.6f, 1f),
            });
        _lr.colorGradient = grad;
    }

    /// <summary>重新生成锯齿顶点（闪电路径）</summary>
    private void RefreshPoints()
    {
        Vector3 dir = (_to - _from).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

        _lr.SetPosition(0, _from);
        _lr.SetPosition(_segments, _to);

        for (int i = 1; i < _segments; i++)
        {
            float t = (float)i / _segments;
            Vector3 point = Vector3.Lerp(_from, _to, t);
            float j = (Random.value - 0.5f) * 2f * _jitter;
            point += perp * j;
            _lr.SetPosition(i, point);
        }
    }
}

/// <summary>
/// 命中辉光运行时组件 — 快速放大并淡出。
/// 泛用视觉组件，可由任意需要命中闪光的效果类复用。
/// </summary>
public class ChainLightningHitGlowRuntime : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _size;
    private float _duration;
    private float _elapsed;

    public void Init(SpriteRenderer sr, float size, float duration)
    {
        _sr = sr;
        _size = size;
        _duration = Mathf.Max(0.05f, duration);
        _elapsed = 0f;
        transform.localScale = Vector3.one * _size;
    }

    private void Update()
    {
        if (_sr == null) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // 快速放大 + 淡出
        transform.localScale = Vector3.one * Mathf.Lerp(_size, _size * 2.5f, t);
        var c = _sr.color;
        c.a = Mathf.Lerp(1f, 0f, t * t);
        _sr.color = c;
    }
}
