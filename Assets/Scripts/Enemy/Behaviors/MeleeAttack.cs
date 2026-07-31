using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MeleeAttack : MonoBehaviour, IAttackBehavior
{
    [Header("近战攻击")]
    public float damage = 10f;
    public float attackCooldown = 1.0f;

    private float _lastAttackTime = -10f;

    public bool TryAttack(Transform target)
    {
        if (Time.time < _lastAttackTime + attackCooldown) return false;
        if (target == null) return false;

        var dmg = target.GetComponent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(damage);
            _lastAttackTime = Time.time;
            return true;
        }

        return false;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time < _lastAttackTime + attackCooldown) return;

        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            _lastAttackTime = Time.time;
        }
    }
}
