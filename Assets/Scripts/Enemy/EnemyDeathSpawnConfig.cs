using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人死亡生成配置 — 控制敌人在死亡时按概率生成哪些额外单位。
/// 支持多组生成条目，每组可指定：预制体、生成概率、排除的敌人名（防止自身繁殖）、是否排除 Boss。
/// 放在 Resources/EnemyDeathSpawnConfig.asset，运行时由 EnemyDeathSpawner 自动加载。
/// </summary>
[CreateAssetMenu(fileName = "EnemyDeathSpawnConfig", menuName = "Game/Enemy Death Spawn Config")]
public class EnemyDeathSpawnConfig : ScriptableObject
{
    [Tooltip("死亡生成条目列表")]
    public List<DeathSpawnEntry> entries = new List<DeathSpawnEntry>();
}

/// <summary>单组死亡生成条目</summary>
[Serializable]
public class DeathSpawnEntry
{
    [Tooltip("生成预制体")]
    public GameObject prefab;

    [Tooltip("生成概率（0~1）")]
    [Range(0f, 1f)]
    public float spawnChance = 0.15f;

    [Tooltip("排除的敌人 displayName 列表（这些敌人死亡不触发此条目，防止自身繁殖等）")]
    public List<string> excludeNames = new List<string>();

    [Tooltip("是否排除 Boss 死亡触发")]
    public bool excludeBoss = true;

    [Tooltip("生成的散布半径")]
    public float spawnRadius = 0f;
}
