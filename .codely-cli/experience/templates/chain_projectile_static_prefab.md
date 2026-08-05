# 链式投射物 + 静态 Prefab 引用模式

## 用途
实现"命中后弹跳到下一个最近目标"的链式投射物（连锁闪电、噬菌体跳跃、弹射子弹）。核心难点：投射物命中目标后需要生成"下一跳"投射物，但预制体无法在自身代码里直接引用自身资产。

## 核心思路
**用静态字段缓存预制体引用**：由 SkillEffect（工厂入口）在发射第一枚时把预制体塞进投射物的静态字段，之后链式跳转时直接用静态字段生成新实例，绕开 Unity 预制体自引用问题。

## 组件代码
```csharp
// 接口：技能效果只依赖接口，不耦合具体投射物
public interface IChainProjectile
{
    // 命中当前目标后，把"下一跳"交给投射物自己处理
    void InitNext(GameObject nextTarget, float damage);
}

// 投射物：命中后搜索最近敌人并链式生成
public class ChainProjectile : MonoBehaviour, IChainProjectile
{
    /// <summary>静态预制体引用 — 由 SkillEffect 在发射时赋值，避免自引用</summary>
    public static GameObject S_ChainPrefab;

    [SerializeField] private float _chainRadius = 4f;
    [SerializeField] private int _maxHops = 3;
    [SerializeField] private float _damageFalloff = 0.8f;

    private int _hops;
    private float _damage;

    public void Init(Vector2 dir, float speed, float damage, int hops = 0)
    {
        _hops = hops;
        _damage = damage;
        GetComponent<Rigidbody2D>().velocity = dir * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;
        other.GetComponent<IDamageable>()?.TakeDamage(_damage);

        if (_hops >= _maxHops) { Destroy(gameObject); return; }

        // 找最近敌人作为下一跳
        var next = FindNearestEnemy(other.transform.position, _chainRadius);
        if (next == null) { Destroy(gameObject); return; }

        // 用静态字段生成下一枚，沿命中点→下一目标方向飞出
        var go = Instantiate(S_ChainPrefab, other.transform.position, Quaternion.identity);
        go.GetComponent<ChainProjectile>().Init(
            (next.position - other.transform.position).normalized,
            8f, _damage * _damageFalloff, _hops + 1);
    }

    private static Transform FindNearestEnemy(Vector3 pos, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius);
        Transform best = null; float bestDist = float.MaxValue;
        foreach (var h in hits)
        {
            if (!h.CompareTag("Enemy")) continue;
            float d = Vector2.Distance(pos, h.transform.position);
            if (d < bestDist) { bestDist = d; best = h.transform; }
        }
        return best;
    }
}

// SkillEffect：发射时把预制体塞进静态字段
public class ChainProjectileEffect : SkillEffectBase
{
    public GameObject projectilePrefab;
    public int maxHops = 3;

    public override void Execute(...)
    {
        ChainProjectile.S_ChainPrefab = projectilePrefab; // 关键：写静态字段
        var go = Instantiate(projectilePrefab, casterPos, Quaternion.identity);
        go.GetComponent<ChainProjectile>().Init(dir, speed, damage, 0);
    }
}
```

## 关键要点
- 静态字段在场景切换/PlayMode 重进后需重新赋值，SkillEffect 每次施放都写一次最稳妥
- 伤害随跳数递减（`_damageFalloff`），避免链式技能无限放大
- 每次跳转都用 `OverlapCircle` 搜索"当前命中点周围"而非全局，性能可控
- 接口（`IChainProjectile`）+ 静态字段解耦：SkillEffect 不知道投射物内部实现

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
