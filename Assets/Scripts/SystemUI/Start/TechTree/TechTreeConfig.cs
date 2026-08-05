using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 科技树配置 (ScriptableObject) — 只保存根节点（无前置的节点）。
/// 加载时从根节点开始通过 nextNodes 递归遍历整棵树。
/// 修改/扩展科技树只需增删 TechTreeNodeData 资产并在根节点列表中配置引用。
/// 右键 -> Create -> TechTree -> Tech Tree Config 创建。
/// </summary>
[CreateAssetMenu(menuName = "TechTree/Tech Tree Config", fileName = "TechTreeConfig")]
public class TechTreeConfig : ScriptableObject
{
    [Header("根节点列表")]
    [Tooltip("没有前置节点的根节点，科技树从此处开始遍历")]
    [SerializeField] private TechTreeNodeData[] _rootNodes;

    /// <summary>根节点列表</summary>
    public TechTreeNodeData[] RootNodes => _rootNodes;

    /// <summary>从根节点 BFS 遍历获取所有节点</summary>
    public List<TechTreeNodeData> GetAllNodes()
    {
        var result = new List<TechTreeNodeData>();
        var visited = new HashSet<TechTreeNodeData>();
        var queue = new Queue<TechTreeNodeData>();

        if (_rootNodes == null) return result;

        foreach (var root in _rootNodes)
        {
            if (root != null && visited.Add(root))
                queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);

            if (node.NextNodes != null)
            {
                foreach (var next in node.NextNodes)
                {
                    if (next != null && visited.Add(next))
                        queue.Enqueue(next);
                }
            }
        }

        return result;
    }

    /// <summary>按 ID 查找节点（BFS 遍历）</summary>
    public TechTreeNodeData GetNodeById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var node in GetAllNodes())
        {
            if (node.NodeId == id) return node;
        }
        return null;
    }

    /// <summary>判断节点 ID 是否存在</summary>
    public bool ContainsNode(string id) => GetNodeById(id) != null;
}
