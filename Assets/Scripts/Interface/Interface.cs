using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一伤害接收接口
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}

/// <summary>
/// 统一治疗接收接口
/// </summary>
public interface IHealable
{
    void Heal(float amount);
}

/// <summary>
/// Buff 目标接口 —— 标记可接收 Buff 的实体。
/// 由 Player/Enemy 实现，提供 BuffManager 访问入口。
/// </summary>
public interface IBuffTarget
{
    BuffManager BuffManager { get; }
}

