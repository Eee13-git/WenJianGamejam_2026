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
