using UnityEngine;

/// <summary>
/// 科技树节点配置 (ScriptableObject) — 每个节点一个 .asset 文件，类似 SkillData。
/// 节点间通过 previousNodes / nextNodes 直接引用其他 TechTreeNodeData 资产。
/// effect 字段引用 TechTreeEffectBase 资产，实现策略模式。
/// 右键 -> Create -> TechTree -> Node Data 创建。
/// </summary>
[CreateAssetMenu(menuName = "TechTree/Node Data", fileName = "TechTreeNode")]
public class TechTreeNodeData : ScriptableObject
{
    [Header("标识")]
    [Tooltip("唯一标识符（用于 PlayerPrefs 持久化）")]
    [SerializeField] private string _nodeId;

    [Tooltip("显示名称")]
    [SerializeField] private string _displayName;

    [TextArea(2, 4)]
    [Tooltip("节点描述")]
    [SerializeField] private string _description;

    [Header("消耗")]
    [Tooltip("解锁所需科技点")]
    [SerializeField] private int _cost = 1;

    [Header("UI布局")]
    [Tooltip("在科技树面板中的位置 (x=列, y=行)")]
    [SerializeField] private Vector2 _position = Vector2.zero;

    [Header("链表结构")]
    [Tooltip("前置节点（全部解锁后才可解锁本节点）")]
    [SerializeField] private TechTreeNodeData[] _previousNodes;

    [Tooltip("后继节点（本节点解锁后才可解锁后继节点）")]
    [SerializeField] private TechTreeNodeData[] _nextNodes;

    [Header("效果")]
    [Tooltip("解锁后应用的效果策略")]
    [SerializeField] private TechTreeEffectBase _effect;

    // ========== 只读属性 ==========

    public string NodeId => _nodeId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public int Cost => _cost;
    public Vector2 Position => _position;
    public TechTreeNodeData[] PreviousNodes => _previousNodes;
    public TechTreeNodeData[] NextNodes => _nextNodes;
    public TechTreeEffectBase Effect => _effect;
}
