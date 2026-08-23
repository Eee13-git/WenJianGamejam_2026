using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 暗仪刺刀运行时组件 — 潜行 + 连锁追击：
/// 1) 潜行：施法者半透明 + 无敌
/// 2) 每跳找最近目标（敌人或敌方投射物）→ 瞬移贴近 → 命中（伤敌/清弹）
/// 3) 最多 maxChains 跳后到达终点，沿整条路径生成闪电链连接并结算伤害
/// 泛用组件：所有数值/视觉由 DarkArtsSkillEffect 参数驱动。
/// </summary>
public class DarkArtsRuntime : MonoBehaviour
{
    private ISkillCaster _caster;
    private GameObject _casterGO;
    private SpriteRenderer _casterSprite;
    private Color _baseColor;
    private Projectile.OwnerType _ownerType;
    private DamageImmunity _immunity;

    private int _maxChains;
    private float _searchRadius;
    private float _jumpDelay;
    private float _stealthAlpha;
    private float _damage;

    // 闪电链视觉参数
    private Material _arcMaterial;
    private int _arcSegments;
    private float _arcJitter;
    private float _arcWidth;
    private float _arcDuration;
    private Color _arcColor;
    private Sprite _hitGlowSprite;
    private Color _hitGlowColor;
    private float _hitGlowSize;
    private float _hitGlowDuration;

    // 路径点（追击过的目标位置，用于终点闪电链）
    private readonly List<Vector3> _pathPoints = new List<Vector3>();

    public void Initialize(ISkillCaster caster,
        int maxChains, float searchRadius, float jumpDelay,
        float stealthAlpha, float damage,
        Material arcMaterial, int arcSegments, float arcJitter, float arcWidth, float arcDuration, Color arcColor,
        Sprite hitGlowSprite, Color hitGlowColor, float hitGlowSize, float hitGlowDuration)
    {
        _caster = caster;
        _casterGO = caster.CasterTransform.gameObject;
        _ownerType = caster.GetOwnerType();
        _maxChains = Mathf.Max(1, maxChains);
        _searchRadius = searchRadius;
        _jumpDelay = Mathf.Max(0.02f, jumpDelay);
        _stealthAlpha = Mathf.Clamp01(stealthAlpha);
        _damage = damage;

        _arcMaterial = arcMaterial;
        _arcSegments = Mathf.Max(2, arcSegments);
        _arcJitter = arcJitter;
        _arcWidth = arcWidth;
        _arcDuration = arcDuration;
        _arcColor = arcColor;
        _hitGlowSprite = hitGlowSprite;
        _hitGlowColor = hitGlowColor;
        _hitGlowSize = hitGlowSize;
        _hitGlowDuration = hitGlowDuration;

        _casterSprite = _casterGO.GetComponentInChildren<SpriteRenderer>();
        _baseColor = _casterSprite != null ? _casterSprite.color : Color.white;

        // 潜行：半透明 + 无敌。停用 DamageImmunity 闪烁（否则每帧把 alpha 拉回 1），自己保持半透明
        _immunity = _casterGO.GetComponent<DamageImmunity>();
        if (_immunity == null) _immunity = _casterGO.AddComponent<DamageImmunity>();
        _immunity.GrantImmunity(999f); // 潜行期间持续无敌
        _immunity.enabled = false;     // 只保留 IsImmune 判定，关闭闪烁

        if (_casterSprite != null)
        {
            var c = _casterSprite.color;
            c.a = _stealthAlpha;
            _casterSprite.color = c;
        }

        StartCoroutine(Run());
    }

    private void Update()
    {
        // 潜行期间持续保持半透明（防止其它系统改色）
        if (_casterSprite != null && _immunity != null && !_immunity.enabled)
        {
            var c = _casterSprite.color;
            if (Mathf.Abs(c.a - _stealthAlpha) > 0.01f)
            {
                c.a = _stealthAlpha;
                _casterSprite.color = c;
            }
        }
    }

    private IEnumerator Run()
    {
        // 潜行前摇：让玩家看清进入潜行
        yield return new WaitForSeconds(0.1f);

        // 路径起点 = 施法者当前位置
        Vector3 origin = _casterGO.transform.position;
        _pathPoints.Add(origin);

        string enemyTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        var hitSet = new HashSet<Collider2D>();
        var targets = new List<Transform>();   // 本次连锁命中的所有目标（终点统一结算）

        // 关键：施法者是 Dynamic 刚体时，直接改 transform.position 会被物理系统无视（下一帧拉回），
        // 必须通过 rb.position 瞬移才能真正移动角色（否则"穿梭"不生效，技能看起来没放）。
        var casterRb = _casterGO.GetComponent<Rigidbody2D>();

        for (int i = 0; i < _maxChains; i++)
        {
            // 找最近目标（敌人或敌方投射物）
            Transform target = FindNearestTarget(origin, enemyTag, hitSet);
            if (target == null) break;

            // 瞬移到目标位置（贴近）：Dynamic 刚体用 rb.position，否则用 transform
            if (casterRb != null && casterRb.bodyType == RigidbodyType2D.Dynamic)
            {
                casterRb.velocity = Vector2.zero;   // 清速度，防瞬移后被原速度带离
                casterRb.position = target.position;
            }
            else
                _casterGO.transform.position = target.position;

            // 连锁期只记录目标、不结算伤害（伤害在到达终点后统一结算）
            targets.Add(target);

            // 排除本次撞击的目标：下一跳找最近的"其他"目标（否则会连续打同一个敌人）
            var hitCol = target.GetComponent<Collider2D>();
            if (hitCol != null)
                hitSet.Add(hitCol);

            origin = target.position;
            _pathPoints.Add(origin);

            if (i < _maxChains - 1)
                yield return new WaitForSeconds(_jumpDelay);
        }

        // 到达终点：统一结算全部目标伤害 + 清弹，再沿整条路径生成闪电链连接
        foreach (var t in targets)
            HitTarget(t);
        for (int i = 0; i < _pathPoints.Count - 1; i++)
            SpawnArc(_pathPoints[i], _pathPoints[i + 1]);

        // 结束：恢复透明度 + 音效 + 清理
        Restore();
        yield return new WaitForSeconds(_arcDuration);
        Destroy(gameObject);
    }

    /// <summary>在范围内查找最近且未命中的目标（敌人或敌方投射物）</summary>
    private Transform FindNearestTarget(Vector3 origin, string enemyTag, HashSet<Collider2D> hitSet)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, _searchRadius, ~0);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hitSet.Contains(hit)) continue;

            // 敌人/随从：tag 匹配即可（随从同化后 tag=Player，也是有效目标）
            if (hit.CompareTag(enemyTag))
            {
                var ec = hit.GetComponent<EnemyCore>();
                if (ec != null)
                {
                    // 敌人/随从：排除死亡、玩家阵营时跳过已同化的随从（自己人）
                    if (ec.IsDead) continue;
                    // 施法者是玩家阵营时，跳过已同化的随从（自己人）；Boss 阵营时随从是目标（允许）
                    if (_ownerType == Projectile.OwnerType.Player && ec.IsAssimilated) continue;
                }
                else
                {
                    // 玩家本体（无 EnemyCore）：必须是存活玩家才是有效目标
                    var ps = hit.GetComponent<PlayerStats>();
                    if (ps == null || ps.IsDead) continue;
                }
            }
            // 投射物：敌方阵营的投射物（可被追击清除）
            else
            {
                var proj = hit.GetComponent<Projectile>();
                if (proj == null) continue;
                if (proj.Owner == _ownerType) continue; // 同阵营不追
            }

            float sqr = (hit.transform.position - origin).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = hit.transform;
            }
        }
        return best;
    }

    /// <summary>命中处理：敌人造成伤害，敌方投射物销毁（清弹幕）</summary>
    private void HitTarget(Transform target)
    {
        if (target == null) return;

        var enemy = target.GetComponent<EnemyCore>();
        if (enemy != null)
        {
            enemy.Health?.TakeDamage(_damage);
            SpawnHitGlow(target.position);
            return;
        }

        // 玩家本体（敌人阵营施放时 tag=Player 的目标）→ 直接扣玩家生命
        var player = target.GetComponent<PlayerStats>();
        if (player != null)
        {
            player.TakeDamage(_damage);
            SpawnHitGlow(target.position);
            return;
        }

        var proj = target.GetComponent<Projectile>();
        if (proj != null)
        {
            SpawnHitGlow(target.position);
            Destroy(proj.gameObject);   // 清除敌方弹幕
        }
    }

    /// <summary>恢复施法者透明度 + 清除潜行无敌（避免 999s 剩余无敌持续白闪）</summary>
    private void Restore()
    {
        if (_casterSprite != null)
        {
            var c = _baseColor;
            c.a = 1f;
            _casterSprite.color = c;
        }
        // 清除潜行授予的长无敌 + 重新启用 DamageImmunity（remainingTime=0 不再闪烁），
        // 同时让 Update 的"保持半透明"守卫（!_immunity.enabled）停止生效
        if (_immunity != null)
        {
            _immunity.ClearImmunity();
            _immunity.enabled = true;
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("dark_arts_dagger");
    }

    // ── 视觉：闪电弧 + 命中辉光（复用泛用组件）──

    private void SpawnArc(Vector3 from, Vector3 to)
    {
        if (_arcMaterial == null) return;
        if (Vector3.Distance(from, to) < 0.01f) return;

        var go = new GameObject("DarkArtsArc");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = _arcSegments + 1;
        lr.startWidth = _arcWidth;
        lr.endWidth = _arcWidth * 0.6f;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.sortingOrder = 25;
        lr.material = _arcMaterial;

        var arc = go.AddComponent<ChainLightningArcRuntime>();
        arc.useGradient = false;   // 暗仪刺刀：纯色暗紫弧，不渐变
        arc.Init(lr, from, to, _arcSegments, _arcJitter, _arcDuration, _arcColor, 3);

        Destroy(go, _arcDuration);
    }

    private void SpawnHitGlow(Vector3 pos)
    {
        var go = new GameObject("DarkArtsHitGlow");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 26;
        if (_hitGlowSprite != null)
        {
            sr.sprite = _hitGlowSprite;
            sr.color = _hitGlowColor;
        }
        var glow = go.AddComponent<ChainLightningHitGlowRuntime>();
        glow.Init(sr, _hitGlowSize, _hitGlowDuration);
        Destroy(go, _hitGlowDuration);
    }

    private void OnDestroy()
    {
        // 兜底恢复（防意外销毁残留半透明/无敌）
        if (_casterSprite != null)
        {
            var c = _baseColor;
            c.a = 1f;
            _casterSprite.color = c;
        }
        if (_immunity != null)
        {
            _immunity.ClearImmunity();
            _immunity.enabled = true;
        }
    }
}
