using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 接触命中触发 Buff — 挂载者（敌人）的接触伤害命中目标时，给目标挂指定 debuff。
/// 适用于 "触碰玩家给玩家挂 Debuff" 的敌人（如细胞衰老病灶）。
/// 通过被动技能（ApplyBuffSkillEffect + 永久 buff）挂到敌人身上；
/// OnApply 订阅 EnemyCore.OnAnyContactHit，命中时给目标 ApplyBuff，OnRemove 取消订阅。
/// 泛用类：debuff、触发概率均可配置，可被多个"触碰挂 debuff"类敌人复用。
/// 右键 -> Create -> Game -> Buff Effect -> On Contact Hit
/// </summary>
[CreateAssetMenu(fileName = "OnContactHitBuff", menuName = "Game/Buff Effect/On Contact Hit")]
public class OnContactHitBuff : BuffEffectBase
{
    [Tooltip("接触命中时给目标挂载的 debuff")]
    public BuffData contactDebuff;

    [Tooltip("触发概率 (0-1)，1 = 必定触发")]
    [Range(0f, 1f)] public float procChance = 1f;

    private static readonly Dictionary<BuffInstance, Action<EnemyCore, GameObject>> _handlers
        = new Dictionary<BuffInstance, Action<EnemyCore, GameObject>>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        Action<EnemyCore, GameObject> handler = null;
        handler = (enemy, hitTarget) =>
        {
            // 只处理挂载者自身的接触命中
            if (enemy.gameObject != target) return;

            if (Random.value > procChance) return;

            if (contactDebuff != null)
            {
                var buffMgr = hitTarget.GetComponent<BuffManager>();
                if (buffMgr != null)
                    buffMgr.ApplyBuff(contactDebuff, enemy.gameObject);
            }
        };

        _handlers[buff] = handler;
        EnemyCore.OnAnyContactHit += handler;
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime) { }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (!_handlers.TryGetValue(buff, out var handler)) return;
        EnemyCore.OnAnyContactHit -= handler;
        _handlers.Remove(buff);
    }
}
