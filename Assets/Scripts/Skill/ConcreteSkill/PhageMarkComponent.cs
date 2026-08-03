using System.Collections.Generic;
using UnityEngine;

/// Phage Mark component — DOT + death chain reaction.
public class PhageMarkComponent : MonoBehaviour
{
    private float _damagePerTick;
    private float _tickInterval = 1f;
    private int _ticksRemaining = 4;
    private float _tickTimer;
    private int _chainCount = 3;
    private float _chainSearchRadius = 8f;
    private float _chainProjectileSpeed = 6f;
    private GameObject _phageProjectilePrefab;
    private Projectile.OwnerType _ownerType;
    private float _casterAttackStrength;
    private float _damageMultiplier;
    private EnemyCore _enemyCore;
    private GameObject _markVisualInstance;
    private GameObject _markVisualPrefab;

    public void Initialize(float totalDamage, int totalTicks, float tickInterval,
        int chainCount, float chainRadius, float chainProjectileSpeed,
        GameObject phagePrefab, Projectile.OwnerType ownerType,
        float casterAttackStrength, float damageMultiplier,
        GameObject markVisualPrefab)
    {
        _ticksRemaining = totalTicks;
        _damagePerTick = totalDamage / totalTicks;
        _tickInterval = tickInterval;
        _tickTimer = 0f;
        _chainCount = chainCount;
        _chainSearchRadius = chainRadius;
        _chainProjectileSpeed = chainProjectileSpeed;
        _phageProjectilePrefab = phagePrefab;
        _ownerType = ownerType;
        _casterAttackStrength = casterAttackStrength;
        _damageMultiplier = damageMultiplier;
        _markVisualPrefab = markVisualPrefab;

        _enemyCore = GetComponent<EnemyCore>();
        if (_enemyCore != null)
            _enemyCore.OnDied += OnEnemyDied;
        else
            Debug.LogError("[PhageMark] EnemyCore not found on target!");

        if (markVisualPrefab != null && _enemyCore != null)
        {
            _markVisualInstance = Instantiate(markVisualPrefab, transform);
            _markVisualInstance.transform.localPosition = new Vector3(0, 1.2f, 0);
        }

        Debug.Log($"[PhageMark] Initialized on {gameObject.name}, dmg/tick={_damagePerTick}, ticks={_ticksRemaining}");
    }

    void Update()
    {
        if (_ticksRemaining <= 0) return;

        _tickTimer += Time.deltaTime;
        while (_tickTimer >= _tickInterval && _ticksRemaining > 0)
        {
            _tickTimer -= _tickInterval;
            _ticksRemaining--;
            ApplyTick();
        }

        if (_ticksRemaining <= 0)
            OnMarkExpired();
    }

    private void ApplyTick()
    {
        if (_enemyCore == null || _enemyCore.IsDead) return;
        _enemyCore.TakeDamage(_damagePerTick);
    }

    private void OnMarkExpired()
    {
        Cleanup();
    }

    private void OnEnemyDied()
    {
        Debug.Log($"[PhageMark] OnEnemyDied on {gameObject.name} — triggering chain");
        SpawnChainPhages();
        Cleanup();
    }

    private void SpawnChainPhages()
    {
        // Prefer static reference (GUID-based from skill effect), fallback to instance field
        GameObject spawnPrefab = BacteriophageProjectile.S_PhagePrefab;
        if (spawnPrefab == null) spawnPrefab = _phageProjectilePrefab;

        if (spawnPrefab == null)
        {
            Debug.LogWarning("[PhageMark] No phage prefab available — chain aborted");
            return;
        }

        var hits = Physics2D.OverlapCircleAll(transform.position, _chainSearchRadius);
        List<Transform> targets = new List<Transform>();

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy") && hit.transform != transform)
                targets.Add(hit.transform);
        }

        Debug.Log($"[PhageMark] Chain: found {targets.Count} enemies in radius {_chainSearchRadius}");

        int spawnCount = Mathf.Min(_chainCount, targets.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject go = Instantiate(spawnPrefab, transform.position, Quaternion.identity);
            var bp = go.GetComponent<BacteriophageProjectile>();
            if (bp != null)
            {
                float chainDamage = _ticksRemaining > 0 ? _damagePerTick * 4f : _damagePerTick * 2f;
                bp.Initialize(targets[i], _chainProjectileSpeed,
                    _casterAttackStrength, _damageMultiplier,
                    chainDamage, _ownerType);
                Debug.Log($"[PhageMark] Spawned chain phage → {targets[i].name}, damage={chainDamage}");
            }
        }
    }

    private void Cleanup()
    {
        if (_enemyCore != null)
            _enemyCore.OnDied -= OnEnemyDied;

        if (_markVisualInstance != null)
            Destroy(_markVisualInstance);

        Destroy(this);
    }

    void OnDestroy()
    {
        Cleanup();
    }
}
