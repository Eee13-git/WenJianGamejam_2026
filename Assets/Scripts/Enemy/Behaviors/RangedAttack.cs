using UnityEngine;

/// <summary>
/// 远程攻击行为：定时朝玩家发射投射物。
/// 投射物预制体需挂有 Projectile 组件，RangedAttack 通过 Initialize() 配置其飞行参数。
/// </summary>
public class RangedAttack : MonoBehaviour, IAttackBehavior
{
    [Header("远程攻击")]
    public float damage = 8f;
    [Tooltip("两次攻击间隔（秒）")]
    public float attackCooldown = 1.5f;
    [Tooltip("投射物预制体（需挂有 Projectile 组件）")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 6f;
    [Tooltip("投射物生成偏移（相对敌人位置）")]
    public Vector2 spawnOffset = Vector2.up * 0.5f;
    public float projectileLifetime = 3f;
    [Tooltip("投射物阵营：Enemy=命中Player / Player=命中Enemy（随从同化后应为Player）")]
    public Projectile.OwnerType projectileOwnerType = Projectile.OwnerType.Enemy;

    private float _lastAttackTime = -10f;

    public bool TryAttack(Transform target)
    {
        if (Time.time < _lastAttackTime + attackCooldown) return false;
        if (target == null) return false;
        if (projectilePrefab == null) return false;

        Vector2 dir = (target.position - transform.position).normalized;
        Vector2 spawnPos = (Vector2)transform.position + spawnOffset;

        GameObject obj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        Projectile proj = obj.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(dir, projectileSpeed, damage, projectileOwnerType, gameObject);
        }
        else
        {
            // 回退：无 Projectile 组件则手动销毁
            Destroy(obj, projectileLifetime);
        }

        _lastAttackTime = Time.time;
        return true;
    }
}
