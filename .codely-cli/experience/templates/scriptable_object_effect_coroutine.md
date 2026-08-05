# ScriptableObject 技能效果启动协程模式

## 用途
`SkillEffectBase`（ScriptableObject）这种无 MonoBehaviour 的配置资产，在 `Execute()` 中需要延迟/多帧逻辑（逐跳连锁、预警延时、召唤间隔）时如何启动协程。

## 核心思路
ScriptableObject 不能挂协程，但 `ISkillCaster` 的 `CasterTransform` 上必有 MonoBehaviour（施法者本体或管理器）。取它的组件启动协程即可。

## 代码
```csharp
public abstract class SkillEffectBase : ScriptableObject
{
    public abstract void Execute(ISkillCaster caster, Vector2 direction,
        float damageMultiplier, Projectile.OwnerType ownerType);
}

// 效果子类
public class ChainEffect : SkillEffectBase
{
    public override void Execute(ISkillCaster caster, Vector2 direction,
        float damageMultiplier, Projectile.OwnerType ownerType)
    {
        if (caster == null || caster.CasterTransform == null) return;

        // 关键：从施法者身上取 MonoBehaviour 作为协程宿主
        MonoBehaviour runner = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (runner == null)
        {
            Debug.LogWarning("caster has no MonoBehaviour to run coroutine!");
            return;
        }

        runner.StartCoroutine(ChainRoutine(caster, damageMultiplier, ownerType));
    }

    private System.Collections.IEnumerator ChainRoutine(
        ISkillCaster caster, float damageMultiplier, Projectile.OwnerType ownerType)
    {
        float damage = caster.GetAttackStrength() * damageMultiplier;
        string targetTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";

        // 每跳间隔
        for (int i = 0; i < 5; i++)
        {
            // ... 查找最近目标、造成伤害、生成特效
            yield return new WaitForSeconds(0.06f);
        }
    }
}
```

## 关键点
- `GetComponent<MonoBehaviour>()` 在任意 GameObject 上都能拿到组件（MonoBehaviour 是组件基类），通用且安全
- 用 `targetTag` 按 `ownerType` 区分阵营（Player 打 "Enemy"，Enemy 打 "Player"）
- 伤害计算：`caster.GetAttackStrength() * damageMultiplier`（damageMultiplier 含等级加成，由 SkillInstance 传入）
- 跳过已死目标：命中前检查目标的 `IsDead`（通过门面组件获取）
- 协程内创建的临时 GameObject 用 `Destroy(go, duration)` 自动清理

## 适用场景
- 连锁/弹跳类技能（逐跳 WaitForSeconds）
- 预警型 AoE（延迟伤害）
- 召唤类技能（间隔生成单位）

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
