using System;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public enum OwnerType { Player, Enemy }

    /// <summary>全局投射物命中事件: (projectile, hitTarget)</summary>
    public static event Action<Projectile, GameObject> OnAnyProjectileHit;

    [Header("子弹参数")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 3f;

    /// <summary>投射物所属主人GameObject（供Buff系统等使用）</summary>
    public GameObject Caster { get; private set; }

    private Vector2 _direction;
    private OwnerType _owner;
    private bool _isInitialized;

    /// <param name="caster">投射物所有者GameObject，可选</param>
    public void Initialize(Vector2 dir, float spd, float dmg, OwnerType owner, GameObject caster = null)
    {
        _direction = dir.normalized;
        speed = spd;
        damage = dmg;
        _owner = owner;
        Caster = caster ?? gameObject;
        _isInitialized = true;

        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (!_isInitialized) return;
        transform.Translate(_direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized) return;

        // 碰墙销毁
        if (other.CompareTag("Obstacles"))
        {
            Destroy(gameObject);
            return;
        }

        // 通过 Tag 区分敌我，通过 IDamageable 接口统一造成伤害
        string targetTag = _owner == OwnerType.Player ? "Enemy" : "Player";
        if (!other.CompareTag(targetTag)) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            OnAnyProjectileHit?.Invoke(this, other.gameObject);
            Destroy(gameObject);
        }
    }

    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}
