using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 闪电链投射物 — 命中后弹跳至下一最近敌人，共传导4次（命中5个敌人）。
/// 实现 IHomingProjectile 供 HomingProjectileSkillEffect 使用。
/// </summary>
public class StreptomycesProjectile : MonoBehaviour, IHomingProjectile
{
    /// <summary>Static prefab reference for chain spawning.</summary>
    public static GameObject S_StreptoPrefab;

    [Header("Chain Settings")]
    [Tooltip("弹跳搜索半径")]
    [SerializeField] private float _chainJumpRadius = 8f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("Hit Effect")]
    [SerializeField] private GameObject _hitEffectPrefab;

    [Header("Chain Lightning Arc")]
    [Tooltip("闪电弧段数（锯齿点数 = segments+1）")]
    [SerializeField] private int _arcSegments = 6;
    [Tooltip("闪电弧最大偏移量")]
    [SerializeField] private float _arcJitter = 0.3f;
    [Tooltip("闪电弧持续时间")]
    [SerializeField] private float _arcDuration = 0.15f;
    [Tooltip("闪电弧宽度")]
    [SerializeField] private float _arcWidth = 0.08f;
    [Tooltip("闪电弧材质（电弧 shader），留空则自动加载）")]
    [SerializeField] private Material _arcMaterial;

    private Transform _target;
    private float _speed = 14f;
    private float _damage;
    private Projectile.OwnerType _ownerType;
    private int _jumpsRemaining = 4;
    private HashSet<int> _alreadyHitIds;

    private bool _initialized;

    public void Initialize(Transform target, float speed,
        float casterAttackStrength, float damageMultiplier,
        float totalDamage, Projectile.OwnerType ownerType)
    {
        _target = target;
        _speed = speed > 0 ? speed : 14f;
        _damage = totalDamage;
        _ownerType = ownerType;
        _initialized = true;

        Destroy(gameObject, 4f);
    }

    /// <summary>
    /// 内部用于链弹跳的初始化。
    /// </summary>
    public void InitializeAsChain(Transform target, float speed, float damage,
        Projectile.OwnerType ownerType, int jumpsRemaining, HashSet<int> alreadyHit)
    {
        _target = target;
        _speed = speed;
        _damage = damage;
        _ownerType = ownerType;
        _jumpsRemaining = jumpsRemaining;
        _alreadyHitIds = alreadyHit;
        _initialized = true;

        Destroy(gameObject, 4f);
    }

    void Update()
    {
        if (!_initialized) return;

        if (_target != null)
        {
            Vector2 dir = (_target.position - transform.position).normalized;
            transform.Translate(dir * _speed * Time.deltaTime, Space.World);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        else
        {
            transform.Translate(Vector2.right * _speed * Time.deltaTime, Space.World);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_initialized) return;

        if (other.CompareTag("Obstacles"))
        {
            DestroySelf();
            return;
        }

        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        if (!other.CompareTag(targetTag)) return;

        // Deal damage
        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(_damage);

        // Mark as hit
        if (_alreadyHitIds == null)
        {
            _alreadyHitIds = new HashSet<int>();
            _alreadyHitIds.Add(gameObject.GetInstanceID()); // skip self projectile
        }
        _alreadyHitIds.Add(other.gameObject.GetInstanceID());

        // Try chain jump
        TryChainJump(other.transform);

        DestroySelf();
    }

    private void TryChainJump(Transform hitTarget)
    {
        if (_jumpsRemaining <= 0) return;

        Transform nextTarget = FindNextTarget(hitTarget.position);
        if (nextTarget == null) return;

        // 生成连锁闪电弧视觉
        SpawnChainLightningArc(hitTarget.position, nextTarget.position);

        // Spawn next chain bolt from the prefab asset
        var go = S_StreptoPrefab != null
            ? Instantiate(S_StreptoPrefab, hitTarget.position, Quaternion.identity)
            : Instantiate(gameObject, hitTarget.position, Quaternion.identity);

        var sb = go.GetComponent<StreptomycesProjectile>();
        if (sb != null)
        {
            sb.InitializeAsChain(nextTarget, _speed, _damage,
                _ownerType, _jumpsRemaining - 1, _alreadyHitIds);
        }
    }

    /// <summary>
    /// 生成锯齿状闪电弧连接两个目标，短暂显示后自动销毁。
    /// </summary>
    private void SpawnChainLightningArc(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        if (distance < 0.01f) return;

        var arcGo = new GameObject("ChainLightningArc");
        arcGo.transform.position = from;

        var lr = arcGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = _arcSegments + 1;
        lr.startWidth = _arcWidth;
        lr.endWidth = _arcWidth * 0.5f;
        lr.numCornerVertices = 0;
        lr.numCapVertices = 2;
        lr.sortingOrder = 25;

        // 使用电弧材质（加法混合）
        Material arcMat = _arcMaterial;
        if (arcMat == null)
        {
            // 从粒子拖尾材质获取电弧 shader 材质
            var trail = transform.Find("TrailParticles");
            if (trail != null)
            {
                var psr = trail.GetComponent<ParticleSystemRenderer>();
                if (psr != null && psr.sharedMaterial != null)
                    arcMat = psr.sharedMaterial;
            }
        }
        if (arcMat == null && _spriteRenderer != null && _spriteRenderer.sharedMaterial != null)
            arcMat = _spriteRenderer.sharedMaterial;

        lr.material = arcMat != null ? arcMat : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        lr.startColor = new Color(0.5f, 1f, 1f, 0.9f);
        lr.endColor = new Color(0.2f, 0.6f, 1f, 0.5f);

        // 生成锯齿点
        Vector2 direction = (to - from).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        lr.SetPosition(0, from);
        lr.SetPosition(_arcSegments, to);

        for (int i = 1; i < _arcSegments; i++)
        {
            float t = (float)i / _arcSegments;
            Vector2 point = Vector2.Lerp(from, to, t);
            float jitter = (Random.value - 0.5f) * 2f * _arcJitter;
            point += perpendicular * jitter;
            lr.SetPosition(i, point);
        }

        Destroy(arcGo, _arcDuration);
    }

    private Transform FindNextTarget(Vector2 origin)
    {
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        var candidates = GameObject.FindGameObjectsWithTag(targetTag);

        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var go in candidates)
        {
            if (go.GetComponent<EnemyCore>() is EnemyCore ec && ec.IsDead) continue;
            // Skip already hit
            if (_alreadyHitIds != null && _alreadyHitIds.Contains(go.GetInstanceID())) continue;
            // Skip self
            if ((Vector2)go.transform.position == origin) continue;

            float d = Vector2.SqrMagnitude((Vector2)go.transform.position - origin);
            if (d < bestDist && d < _chainJumpRadius * _chainJumpRadius)
            {
                bestDist = d;
                best = go.transform;
            }
        }
        return best;
    }

    private void DestroySelf()
    {
        if (_hitEffectPrefab != null)
            Instantiate(_hitEffectPrefab, transform.position, Quaternion.identity);

        var trail = transform.Find("TrailParticles");
        if (trail != null)
        {
            trail.SetParent(null);
            var ps = trail.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trail.gameObject, ps.main.startLifetime.constantMax + 1f);
            }
            else
            {
                Destroy(trail.gameObject, 2f);
            }
        }

        Destroy(gameObject);
    }

    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}
