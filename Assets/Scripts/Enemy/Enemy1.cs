using UnityEngine;

public class Enemy1 : BaseEnemy
{
    [Header("近战属性")]
    [SerializeField] private float damageToPlayer = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    protected override void FixedUpdate()
    {
        if (isDead) return;
        // 检测玩家，在检测范围内则追击
        if (Vector2.Distance(transform.position, playerTarget.position) <= detectionRange)
        {
            MoveTowardsPlayer();
        }
    }

    // 碰撞接触时给玩家造成伤害（通过 IDamageable 接口解耦）
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || Time.time < lastHitTime + attackCooldown) return;
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damageToPlayer);
            lastHitTime = Time.time;
        }
    }
}
