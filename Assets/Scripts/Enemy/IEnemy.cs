using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一敌人对外接口，供生成器、技能及其它系统使用。
/// 重构目标是让外部通过接口而非具体 BaseEnemy 类型交互。
/// </summary>
public interface IEnemy : IDamageable, ISkillCaster
{
    /// <summary>用于外部查询或定位的 Transform 引用</summary>
    Transform EnemyTransform { get; }

    /// <summary>敌人是否已死亡</summary>
    bool IsDead { get; }

    /// <summary>是否已被同化为随从（房间/生成器应排除）</summary>
    bool IsAssimilated { get; }

    /// <summary>敌人当前持有的技能实例（只读）</summary>
    IReadOnlyList<SkillInstance> SkillInstances { get; }

    /// <summary>敌人死亡事件（触发时表示该敌人已死亡/即将销毁）</summary>
    event Action OnDied;

    /// <summary>敌人被同化为随从事件（触发时房间/生成器应从存活列表移除）</summary>
    event Action<IEnemy> OnAssimilated;
}
