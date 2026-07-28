using UnityEngine;

public class Enemy1 : BaseEnemy
{
    [Header("近战攻击")]
    [SerializeField] private float damageToPlayer = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    protected override void FixedUpdate()
    {
        if (isDead) return;

        // 使用智能寻路（A* + 视线检测）
        MoveTowardsPlayer();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || Time.time < lastHitTime + attackCooldown) return;
        PlayerStats player = collision.gameObject.GetComponent<PlayerStats>();
        if (player != null)
        {
            player.TakeDamage(damageToPlayer);
            lastHitTime = Time.time;
        }
    }
}