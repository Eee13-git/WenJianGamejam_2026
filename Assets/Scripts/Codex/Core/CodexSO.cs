using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 图鉴资产基类 — 包含一组图鉴条目。
/// </summary>
public abstract class CodexSO : ScriptableObject
{
    public abstract IReadOnlyList<CodexEntrySO> Entries { get; }
}
