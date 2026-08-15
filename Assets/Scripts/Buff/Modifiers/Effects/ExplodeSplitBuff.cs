using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 泛用死亡分裂 buff 效果 — 死亡触发：死亡瞬间按配置的多组预制体爆出敌人。
/// 每组可指定预制体与数量（如"2 个单球菌 + 1 个双球菌"），带散布半径。
/// 通过被动技能（ApplyBuffSkillEffect + 永久 buff）挂到敌人身上；
/// OnApply 订阅 EnemyCore.OnDied，死亡瞬间触发，OnRemove 取消订阅。
/// 右键 -> Create -> Game -> Buff Effect -> Explode Split
/// </summary>
[CreateAssetMenu(fileName = "ExplodeSplitBuff", menuName = "Game/Buff Effect/Explode Split")]
public class ExplodeSplitBuff : BuffEffectBase
{
    [Header("分裂配置")]
    [Tooltip("死亡时爆出的敌人组（每组一个预制体 + 数量）")]
    public List<SpawnGroup> spawnGroups = new List<SpawnGroup>();

    [Tooltip("爆出散布半径")]
    public float spawnRadius = 1f;

    // 缓存每个 BuffInstance 的死亡处理器（用于取消订阅）
    private static readonly Dictionary<BuffInstance, Action> _handlers
        = new Dictionary<BuffInstance, Action>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var core = target.GetComponent<EnemyCore>();
        if (core == null) return;

        Action handler = null;
        handler = () =>
        {
            OnDeath(target);
            core.OnDied -= handler;
        };

        _handlers[buff] = handler;
        core.OnDied += handler;
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime) { }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (!_handlers.TryGetValue(buff, out var handler)) return;

        var core = target.GetComponent<EnemyCore>();
        if (core != null)
            core.OnDied -= handler;

        _handlers.Remove(buff);
    }

    /// <summary>死亡瞬间：按配置爆出各组敌人（并计入房间敌人计数）</summary>
    private void OnDeath(GameObject caster)
    {
        if (caster == null) return;
        Vector2 pos = caster.transform.position;

        // 找到原敌人所属的房间生成器（原敌人由 EnemySpawner 生成，parent 即其 transform）
        var spawner = caster.transform.parent != null
            ? caster.transform.parent.GetComponentInParent<EnemySpawner>()
            : null;

        foreach (var group in spawnGroups)
        {
            if (group == null || group.prefab == null || group.count <= 0) continue;

            for (int i = 0; i < group.count; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
                var go = UnityEngine.Object.Instantiate(group.prefab, pos + offset, Quaternion.identity, caster.transform.parent);
                go.name = $"Split_{group.prefab.name}_{i}";

                // 计入房间敌人计数，避免房间门提前开启
                spawner?.RegisterEnemy(go);
            }
        }
    }
}

/// <summary>分裂生成组：一个预制体 + 数量</summary>
[Serializable]
public class SpawnGroup
{
    [Tooltip("要生成的敌人预制体")]
    public GameObject prefab;
    [Tooltip("生成数量")]
    public int count = 1;
}
