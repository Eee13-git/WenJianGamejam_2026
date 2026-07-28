using UnityEngine;

public class Projectile : MonoBehaviour
{
    public enum OwnerType { Player, Enemy }

    [Header("子弹参数")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 3f;

    private Vector2 _direction;
    private OwnerType _owner;
    private bool _isInitialized;

    public void Initialize(Vector2 dir, float spd, float dmg, OwnerType owner)
    {
        _direction = dir.normalized;
        speed = spd;
        damage = dmg;
        _owner = owner;
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
            Destroy(gameObject);
        }
    }

    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}
