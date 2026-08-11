using UnityEngine;

/// <summary>
/// 抛出敌人组件 — 冲刺结束时把卷起的敌人沿冲刺方向抛出。
/// 抛出后沿方向飞行，撞到墙/障碍触发二次伤害并落地（恢复 AI）。
/// 泛用"抛出"机制：速度、距离、二次伤害均可配置。
/// </summary>
public class CarriedThrow : MonoBehaviour
{
    private Vector2 _direction;
    private float _speed;
    private float _distance;
    private float _wallDamage;
    private float _traveled;

    /// <summary>初始化抛出（沿方向飞行 distance，撞墙触发二次伤害）</summary>
    public void Init(Vector2 direction, float speed, float distance, float wallDamage)
    {
        _direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.right;
        _speed = Mathf.Max(speed, 1f);
        _distance = Mathf.Max(distance, 0.1f);
        _wallDamage = wallDamage;
        _traveled = 0f;

        // 抛出期间无敌（防止被其他伤害打断）；落地/撞墙后移除
        var immunity = GetComponent<DamageImmunity>();
        if (immunity == null)
            immunity = gameObject.AddComponent<DamageImmunity>();
        immunity.GrantImmunity(10f);
    }

    private void Update()
    {
        // 飞行
        float step = _speed * Time.deltaTime;
        transform.position += (Vector3)(_direction * step);
        _traveled += step;

        // 到达距离 → 落地
        if (_traveled >= _distance)
        {
            Land();
            return;
        }

        // 撞墙检测（前方小范围检测 Obstacles/Wall）
        Vector2 checkPos = (Vector2)transform.position + _direction * 0.2f;
        var hits = Physics2D.OverlapCircleAll(checkPos, 0.15f, ~0);
        var selfCol = GetComponent<Collider2D>();
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (selfCol != null && hit == selfCol) continue;
            if (hit.CompareTag("Obstacles") || hit.CompareTag("Wall"))
            {
                HitWall();
                return;
            }
        }
    }

    /// <summary>撞墙：二次伤害 + 落地</summary>
    private void HitWall()
    {
        // 移除抛出期间的无敌（否则无敌拦截二次伤害）
        var immunity = GetComponent<DamageImmunity>();
        if (immunity != null) Destroy(immunity);

        var health = GetComponent<EnemyStats>();
        health?.TakeDamage(_wallDamage);

        Land();
    }

    /// <summary>落地：恢复正常 AI + collider + 物理，移除本组件</summary>
    private void Land()
    {
        var immunity = GetComponent<DamageImmunity>();
        if (immunity != null) Destroy(immunity);

        var core = GetComponent<EnemyCore>();
        if (core != null)
        {
            if (core.Movement != null) core.Movement.enabled = true;
            if (core.StateMachine != null) core.StateMachine.enabled = true;
        }

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = true;

        Destroy(this);
    }
}
