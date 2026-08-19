using UnityEngine;

/// <summary>
/// 图鉴条目基类 — 每个条目对应一个游戏对象（敌人/技能/道具）。
/// </summary>
public abstract class CodexEntrySO : ScriptableObject
{
    [Tooltip("唯一标识，与对象ID一致（敌人 displayName / skillId / itemId）")]
    public string entryId;

    [Tooltip("图鉴显示名称")]
    public string displayName;

    [TextArea]
    [Tooltip("图鉴描述文本")]
    public string description;
}
