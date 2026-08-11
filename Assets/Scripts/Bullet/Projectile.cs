using System;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public enum OwnerType { Player, Enemy }

    /// <summary>全局投射物命中事件 (projectile, hitTarget)</summary>
    public static event Action<Projectile, GameObject> OnAnyProjectileHit;

    /// <summary>灵体子弹模式：玩家方投射物穿透障碍物</summary>
    public static bool SpiritBulletMode;

    /// <summary>全局弹幕速度倍率（道具效果），1=正常，0.5=半速</summary>
    public static float GlobalSpeedMultiplier = 1f;

    [Header("子弹参数")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 3f;

    /// <summary>投射物所属主人GameObject</summary>
    public GameObject Caster { get; private set; }

    /// <summary>投射物阵营（公开只读，供地面区域等组件读取）</summary>
    public OwnerType Owner => _owner;

    private Vector2 _direction;
    private OwnerType _owner;
    private bool _isInitialized;

    /// <summary>是否被时停冻结</summary>
    public bool IsFrozen { get; private set; }
    private float _frozenLifetimeRemain;

    /// <param name="caster">投射物所有者GameObject，可选</param>
    public void Initialize(Vector2 dir, float spd, float dmg, OwnerType owner, GameObject caster = null)
    {
        _direction = dir.normalized;
        speed = spd;
        damage = dmg;
        _owner = owner;
        Caster = caster ?? gameObject;
        _isInitialized = true;
        IsFrozen = false;

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
        CancelInvoke(nameof(ReturnToPool));
    }

    void Update()
    {
        if (!_isInitialized || IsFrozen) return;
        // 玩家方子弹不受全局减速影响
        float mult = _owner == OwnerType.Player ? 1f : GlobalSpeedMultiplier;
        transform.Translate(_direction * speed * mult * Time.deltaTime, Space.World);
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
            damageable.TakeDamage(damage);
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
