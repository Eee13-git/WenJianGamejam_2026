using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物生成条目 — 定义一种怪物的生成参数
/// </summary>
[Serializable]
public class EnemySpawnEntry
{
    [Tooltip("怪物预制体")]
    public GameObject enemyPrefab;

    [Tooltip("最少生成数量")]
    public int minCount = 1;

    [Tooltip("最多生成数量")]
    public int maxCount = 3;

    [Tooltip("生成权重 (0~1)，权重越高越容易被选中")]
    [Range(0f, 1f)]
    public float weight = 1f;
}

/// <summary>
/// 怪物生成配置 — 房间内怪物生成的总配置
/// </summary>
[Serializable]
public class EnemySpawnConfig
{
    [Tooltip("怪物生成条目列表")]
    public List<EnemySpawnEntry> entries = new();
}
