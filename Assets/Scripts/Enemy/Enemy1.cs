using UnityEngine;

public class Enemy1 : BaseEnemy
{
    [Header("近战攻击")]
    [SerializeField] private float damageToPlayer = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    private PlayerStats _cachedPlayerStats;

    protected override void FixedUpdate()
    {
        if (isDead) return;

        // 驱动技能冷却
        TickSkillCooldowns(Time.fixedDeltaTime);

        // 使用智能寻路（A* + 视线检测）
        MoveTowardsPlayer();

        // 释放技能槽位 0 的技能（冷却自控）
        if (_skillInstances.Count > 0)
            TryCastSkill(0);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || Time.time < lastHitTime + attackCooldown) return;

        if (_cachedPlayerStats == null)
            _cachedPlayerStats = collision.gameObject.GetComponent<PlayerStats>();

        if (_cachedPlayerStats != null)
        {
            _cachedPlayerStats.TakeDamage(damageToPlayer);
            lastHitTime = Time.time;
        }
    }
}