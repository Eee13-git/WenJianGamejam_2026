using UnityEngine;

/// Homing bacteriophage projectile — flies toward target, marks on hit.
public class BacteriophageProjectile : MonoBehaviour, IHomingProjectile
{
    /// <summary>Static prefab reference for chain spawning (avoids Unity prefab self-reference issues).</summary>
    public static GameObject S_PhagePrefab;

    [Header("Projectile Visual")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private Transform _target;
    private float _speed = 6f;
    private float _totalDamage = 40f;
    private Projectile.OwnerType _ownerType;
    private float _casterAttackStrength;
    private float _damageMultiplier;
    private int _totalTicks = 4;
    private float _tickInterval = 1f;
    private int _chainCount = 3;
    private float _chainRadius = 8f;
    private float _chainProjectileSpeed = 6f;

    [Header("Effect Prefabs")]
    [SerializeField] private GameObject _hitEffectPrefab;
    [SerializeField] private GameObject _markVisualPrefab;

    [Header("Chain Reaction")]
    [Tooltip("噬菌体投射物预制体资产（用于死亡传播，非运行时实例）")]
    [SerializeField] private GameObject _phagePrefabAsset;

    private bool _initialized;

    public void Initialize(Transform target, float speed,
        float casterAttackStrength, float damageMultiplier,
        float totalDamage, Projectile.OwnerType ownerType)
    {
        _target = target;
        _speed = speed;
        _totalDamage = totalDamage;
        _ownerType = ownerType;
        _casterAttackStrength = casterAttackStrength;
        _damageMultiplier = damageMultiplier;
        _initialized = true;

        Destroy(gameObject, 5f);
    }

    void Awake()
    {
        // Cache the prefab reference statically (via the serialized _phagePrefabAsset on the prefab)
        if (S_PhagePrefab == null && _phagePrefabAsset != null)
            S_PhagePrefab = _phagePrefabAsset;
    }

    void Update()
    {
        if (!_initialized) return;

        if (_target != null)
        {
            Vector2 direction = (_target.position - transform.position).normalized;
            transform.Translate(direction * _speed * Time.deltaTime, Space.World);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
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

        // Hit the correct target — apply mark
        if (other.transform == _target || _target == null)
        {
            ApplyMark(other.gameObject);
        }

        DestroySelf();
    }

    private void ApplyMark(GameObject enemy)
    {
        if (_phagePrefabAsset == null)
            Debug.LogError("[PhageProjectile] _phagePrefabAsset is NULL — chain will not work!");

        var existing = enemy.GetComponent<PhageMarkComponent>();
        if (existing != null)
        {
            // Refresh: destroy old mark, apply new
            Destroy(existing);
        }

        var mark = enemy.AddComponent<PhageMarkComponent>();
        mark.Initialize(_totalDamage, _totalTicks, _tickInterval,
            _chainCount, _chainRadius, _chainProjectileSpeed,
            _phagePrefabAsset, _ownerType,
            _casterAttackStrength, _damageMultiplier,
            _markVisualPrefab);
    }

    private void DestroySelf()
    {
        if (_hitEffectPrefab != null)
        {
            Instantiate(_hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // Detach trail particles and let them dissipate, then destroy
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
