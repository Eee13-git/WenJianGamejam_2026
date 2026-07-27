using UnityEngine;

public class Enemy1 : BaseEnemy
{
    [Header("近战攻击")]
    [SerializeField] private float damageToPlayer = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    protected override void FixedUpdate()
    {
        if (isDead) return;
        // 靠近玩家（检测范围内才追）
        if (Vector2.Distance(transform.position, playerTarget.position) <= detectionRange)
        {
            MoveTowardsPlayer();
        }
    }

    // 碰撞持续触发（类似以撒的碰触伤害）
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || Time.time < lastHitTime + attackCooldown) return;
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damageToPlayer);
            lastHitTime = Time.time;
        }
    }
}