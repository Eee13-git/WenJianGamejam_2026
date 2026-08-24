using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 双球菌亡语 buff 效果 — 死亡触发：
/// 1) 死亡瞬间爆出 N 个单球菌（继承阵营，攻击玩家）；
/// 2) 在死亡位置生成一团有毒气体（持续 poisonDuration 秒，对范围内玩家造成持续伤害）。
/// 通过被动技能（ApplyBuffSkillEffect + 永久 buff）挂到双球菌身上；
/// OnApply 订阅 EnemyCore.OnDied，死亡瞬间触发，OnRemove 取消订阅。
/// </summary>
[CreateAssetMenu(fileName = "DiplococcusDeathBuff", menuName = "Game/Buff Effect/Diplococcus Death")]
public class DiplococcusDeathBuff : BuffEffectBase
{
    [Header("分裂")]
    [Tooltip("死亡时爆出的单球菌预制体池")]
    public GameObject[] singleCocciPrefabs;
    [Tooltip("爆出数量")]
    public int spawnCount = 2;
    [Tooltip("爆出散布半径")]
    public float spawnRadius = 1f;

    [Header("毒气")]
    [Tooltip("毒气团预制体（含 PoisonGasZone 组件）")]
    public GameObject poisonGasPrefab;
    [Tooltip("毒气半径")]
    public float poisonRadius = 2f;
    [Tooltip("毒气持续时间（秒）")]
    public float poisonDuration = 3f;
    [Tooltip("毒气给目标挂的持续伤害 debuff（DamageOverTimeBuff，Refresh 行为）")]
    public BuffData poisonDebuff;

    // 缓存每个 BuffInstance 的死亡处理器（用于取消订阅）
    private static readonly Dictionary<BuffInstance, System.Action> _handlers
        = new Dictionary<BuffInstance, System.Action>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var core = target.GetComponent<EnemyCore>();
        if (core == null) return;

        System.Action handler = null;
        handler = () =>
        {
            OnDeath(target);

            // 触发后立即解除自身订阅
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
            core.OnDied -= handler;   // 若已触发过则为 no-op

        _handlers.Remove(buff);
    }

    /// <summary>死亡瞬间：爆出单球菌 + 毒气团</summary>
    private void OnDeath(GameObject caster)
    {
        if (caster == null) return;
        Vector2 pos = caster.transform.position;

        SpawnCocci(caster);
        SpawnPoisonGas(pos, caster);
    }

    /// <summary>爆出单球菌（继承敌方阵营，攻击玩家），并计入房间敌人计数</summary>
    private void SpawnCocci(GameObject caster)
    {
        if (singleCocciPrefabs == null || singleCocciPrefabs.Length == 0) return;

        // 找到原敌人所属的房间（门锁由 RoomManager._aliveEnemies 控制）
        var room = caster.transform.parent != null
            ? caster.transform.parent.GetComponentInParent<RoomManager>()
            : null;
        var spawner = caster.transform.parent != null
            ? caster.transform.parent.GetComponentInParent<EnemySpawner>()
            : null;

        for (int i = 0; i < spawnCount; i++)
        {
            var prefab = singleCocciPrefabs[Random.Range(0, singleCocciPrefabs.Length)];
            if (prefab == null) continue;

            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            var go = Object.Instantiate(prefab, (Vector2)caster.transform.position + offset, Quaternion.identity, caster.transform.parent);
            go.name = $"Diplococcus_Spawn_{i}_{prefab.name}";

            // 计入房间敌人计数，避免房间门提前开启
            room?.RegisterEnemy(go);
            spawner?.RegisterEnemy(go);
        }
    }

    /// <summary>生成毒气团（敌方阵营，伤害玩家）</summary>
    private void SpawnPoisonGas(Vector2 pos, GameObject caster)
    {
        if (poisonGasPrefab == null) return;

        var go = Object.Instantiate(poisonGasPrefab, pos, Quaternion.identity);
        go.name = "DiplococcusPoisonGas";

        var zone = go.GetComponent<PoisonGasZone>();
        if (zone != null)
        {
            // 毒气 DOT 伤害 = 死亡者攻击力 × 技能倍率(亡语无等级=1) × 毒气倍率，随攻击力成长
            float attackStrength = caster != null
                ? (caster.GetComponent<EnemyCore>()?.GetAttackStrength() ?? 0f)
                : 0f;

            zone.Initialize(Projectile.OwnerType.Enemy, poisonRadius, poisonDuration, poisonDebuff,
                attackStrength, 1f);
        }
    }
}
