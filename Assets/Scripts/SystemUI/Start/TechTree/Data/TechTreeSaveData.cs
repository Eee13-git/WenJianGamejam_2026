using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 科技树持久化数据 — 序列化结构，包含科技点数和已解锁节点列表。
/// </summary>
[System.Serializable]
public class TechTreeSaveData
{
    public int techPoints;
    public List<string> unlockedNodeIds = new();
}
