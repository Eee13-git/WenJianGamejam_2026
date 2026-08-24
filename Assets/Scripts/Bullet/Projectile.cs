using System;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public enum OwnerType { Player, Enemy }

    /// <summary>全局投射物命中事件 (projectile, hitTarget)</summary>
    public static event Action<Projectile, GameObject> OnAnyProjectileHit;

    /// <summary>灵体子弹模式：玩家方投射物穿透障碍物</summary>
    public static bool SpiritBulletMode;

    /// <summary>跟踪模式：玩家方投射物追踪最近敌人</summary>
    public static bool HomingMode;

    /// <summary>全局弹幕速度倍率（道具效果），1=正常，0.5=半速</summary>
    public static float GlobalSpeedMultiplier = 1f;

    /// <summary>玩家方子弹大小倍率（道具效果），1=正常</summary>
    public static float BulletScaleMultiplier = 1f;

    // === 距离衰减模式（甲亢道具） ===
    /// <summary>距离衰减模式：玩家方子弹伤害和大小随飞行距离衰减</summary>
    public static bool DistanceDamageMode;
    public static float DistanceDamageMaxMult = 3f;
    public static float DistanceDamageMinMult = 0.1f;
    public static float DistanceScaleMaxMult = 1.5f;
    public static float DistanceScaleMinMult = 0.3f;
    public static float DistanceMaxRange = 8f;

    [Header("子弹参数")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 3f;

    /// <summary>投射物所属主人GameObject</summary>
    public GameObject Caster { get; private set; }

    /// <summary>投射物阵营（公开只读，供地面区域等组件读取）</summary>
    public OwnerType Owner => _owner;

    /// <summary>投射物当前伤害（公开只读，供地面区域按施法者攻击力缩放 DOT 等使用）</summary>
    public float Damage => damage;

    /// <summary>投射物所属技能等级（默认1；供区域/触发体组件按等级成长半径/时长等机制参数）</summary>
    public int Level { get; private set; } = 1;

    private Vector2 _direction;
    private OwnerType _owner;
    private bool _isInitialized;
    private Vector3 _spawnPos;
    private Vector3 _baseScale;

    /// <summary>是否被时停冻结</summary>
    public bool IsFrozen { get; private set; }
    private float _frozenLifetimeRemain;

    /// <param name="caster">投射物所有者GameObject，可选</param>
    /// <param name="level">技能等级（默认1），供区域/触发体组件机制成长使用</param>
    public void Initialize(Vector2 dir, float spd, float dmg, OwnerType owner, GameObject caster = null, int level = 1)
    {
        _direction = dir.normalized;
        speed = spd;
        damage = dmg;
        _owner = owner;
        Caster = caster ?? gameObject;
        Level = level;
        _isInitialized = true;
        IsFrozen = false;
        _spawnPos = transform.position;
        _baseScale = transform.localScale;

        // 用 Invoke 延时回收（替代 Destroy(gameObject, lifeTime)）
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), lifeTime);
    }

    /// <summary>冻结投射物（停止移动+暂停生命周期）</summary>
    public void Freeze()
    {
        if (!_isInitialized || IsFrozen) return;
        IsFrozen = true;
        // 记录剩余生命时间并取消自动回收
        _frozenLifetimeRemain = Mathf.Max(lifeTime * 0.5f, 1f);
        CancelInvoke(nameof(ReturnToPool));
    }

    /// <summary>解冻投射物，恢复移动和生命周期</summary>
    public void Unfreeze()
    {
        if (!_isInitialized || !IsFrozen) return;
        IsFrozen = false;
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), _frozenLifetimeRemain);
    }

    /// <summary>重置状态（池化复用时由 ProjectilePool 间接调用）</summary>
    public void ResetState()
    {
        _isInitialized = false;
        _direction = Vector2.zero;
        Caster = null;
        Level = 1;
        transform.localScale = _baseScale;
        CancelInvoke(nameof(ReturnToPool));
    }

    void Update()
    {
        if (!_isInitialized || IsFrozen) return;

        // 跟踪模式：玩家方子弹追踪最近敌人
        if (HomingMode && _owner == OwnerType.Player)
        {
            Transform nearest = FindNearestEnemy(transform.position);
            if (nearest != null)
            {
                Vector2 toTarget = (nearest.position - transform.position).normalized;
                _direction = Vector2.Lerp(_direction, toTarget, 8f * Time.deltaTime).normalized;
            }
        }

        // 玩家方子弹不受全局减速影响
        float mult = _owner == OwnerType.Player ? 1f : GlobalSpeedMultiplier;
        transform.Translate(_direction * speed * mult * Time.deltaTime, Space.World);

        // 玩家方子弹大小：距离衰减或固定倍率
        if (_owner == OwnerType.Player)
        {
            if (DistanceDamageMode)
            {
                float dist = Vector3.Distance(transform.position, _spawnPos);
                float t = Mathf.Clamp01(dist / DistanceMaxRange);
                float scaleMult = Mathf.Lerp(DistanceScaleMaxMult, DistanceScaleMinMult, t);
                transform.localScale = _baseScale * scaleMult * BulletScaleMultiplier;
            }
            else
            {
                transform.localScale = _baseScale * BulletScaleMultiplier;
            }
        }
    }

    private Transform FindNearestEnemy(Vector2 center)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, 4f, ~0);
        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            float d = (hit.transform.position - (Vector3)center).sqrMagnitude;
            if (d < minDist) { minDist = d; nearest = hit.transform; }
        }
        return nearest;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized) return;

        if (other.CompareTag("Obstacles"))
        {
            if (!SpiritBulletMode || _owner != OwnerType.Player)
                ReturnToPool();
            return;
        }

        string targetTag = _owner == OwnerType.Player ? "Enemy" : "Player";

        // 动情腺溢散（互相攻击）：敌人投射物命中其他敌人时，若目标带混淆 debuff 则也造成伤害
        bool confusionHit = false;
        if (_owner == OwnerType.Enemy && other.CompareTag("Enemy"))
        {
            var buffMgr = other.GetComponent<BuffManager>();
            confusionHit = buffMgr != null && buffMgr.HasBuff("gland_confusion");
        }

        if (!other.CompareTag(targetTag) && !confusionHit) return;

        // 护盾拦截：目标架盾中则挡住弹幕（不造成伤害）
        var shield = other.GetComponent<KeratinShieldRuntime>();
        if (shield != null && shield.IsActive)
        {
            shield.TryBlock();
            OnAnyProjectileHit?.Invoke(this, other.gameObject);
            ReturnToPool();
            return;
        }

        // 因子掠夺：命中目标后先偷取其增益 buff（目标存活时才有 buff 可偷）
        var plunder = GetComponent<BuffPlunderOnHit>();
        if (plunder != null)
            plunder.TryPlunder(other.gameObject);

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            float finalDamage = damage;

            // 距离衰减模式：根据飞行距离计算实际伤害
            if (DistanceDamageMode && _owner == OwnerType.Player)
            {
                float dist = Vector3.Distance(transform.position, _spawnPos);
                float t = Mathf.Clamp01(dist / DistanceMaxRange);
                float mult = Mathf.Lerp(DistanceDamageMaxMult, DistanceDamageMinMult, t);
                finalDamage = damage * mult;
            }

            damageable.TakeDamage(finalDamage);
            OnAnyProjectileHit?.Invoke(this, other.gameObject);
        }

        ReturnToPool();
    }

    private void OnBecameInvisible()
    {
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (!_isInitialized) return;
        ResetState();
        ProjectilePool.Return(gameObject);
    }
}
