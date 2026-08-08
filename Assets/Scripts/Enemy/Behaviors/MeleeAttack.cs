using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MeleeAttack : MonoBehaviour, IAttackBehavior
{
    [Header("近战攻击")]
    public float damage = 10f;
    public float attackCooldown = 1.0f;

    /// <summary>
    /// 攻击阵营：Enemy=命中Player / Player=命中Enemy。
    /// 随从同化后 EnemyFollower 会将其设为 Player，避免误伤玩家和友军。
    /// </summary>
    [Tooltip("攻击阵营：Enemy=命中Player / Player=命中Enemy")]
    public Projectile.OwnerType ownerType = Projectile.OwnerType.Enemy;

    private float _lastAttackTime = -10f;

    public bool TryAttack(Transform target)
    {
        if (Time.time < _lastAttackTime + attackCooldown) return false;
        if (target == null) return false;

        // 护盾拦截：目标架盾中则挡住冲撞伤害
        var shield = target.GetComponent<KeratinShieldRuntime>();
        if (shield != null && shield.IsActive)
        {
            shield.TryBlock();
            _lastAttackTime = Time.time;
            return true;
        }

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
        // 根据阵营确定命中 Tag
        string targetTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        if (!other.CompareTag(targetTag)) return;

        if (Time.time < _lastAttackTime + attackCooldown) return;

        // 护盾拦截：目标架盾中则挡住冲撞伤害（不造成伤害）
        var shield = other.GetComponent<KeratinShieldRuntime>();
        if (shield != null && shield.IsActive)
        {
            shield.TryBlock();
            _lastAttackTime = Time.time;
            return;
        }

        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            _lastAttackTime = Time.time;
        }
    }
}
