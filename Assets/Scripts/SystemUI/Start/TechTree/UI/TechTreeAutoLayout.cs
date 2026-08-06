using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 科技树 DAG 自动布局算法。
/// 三步: 层级分配(X) → 重心法减少交叉(Y排序) → 坐标分配(Y精确位置)。
/// 坐标系: 基于 RectTransform pivot=(0,1) anchor=(0,1)，左上角原点，Y向下为负。
/// </summary>
public static class TechTreeAutoLayout
{
    /// <summary>布局参数配置</summary>
    [System.Serializable]
    public struct LayoutConfig
    {
        [Tooltip("节点宽度")]
        public float nodeWidth;

        [Tooltip("节点高度")]
        public float nodeHeight;

        [Tooltip("层间水平间距（含节点宽度）")]
        public float layerSpacingX;

        [Tooltip("层内垂直间距（含节点高度）")]
        public float nodeSpacingY;

        [Tooltip("起始 X 偏移（左边距）")]
        public float originX;

        [Tooltip("起始 Y 偏移（上边距，正值=向下偏移）")]
        public float originY;

        [Tooltip("重心法迭代次数")]
        [Range(1, 10)]
        public int barycenterIterations;

        /// <summary>默认配置</summary>
        public static LayoutConfig Default => new()
        {
            nodeWidth = 160f,
            nodeHeight = 100f,
            layerSpacingX = 240f,
            nodeSpacingY = 140f,
            originX = 120f,
            originY = 60f,
            barycenterIterations = 4,
        };
    }

    // ===== 运行时临时数据 =====

    private class NodeLayoutInfo
    {
        public TechTreeNodeData node;
        public int layer = -1;       // -1 = 未计算
        public int indexInLayer;     // 排序后在该层的位置
        public float barycenter;
    }

    /// <summary>
    /// 计算所有节点的布局位置。
    /// 返回 Dictionary: nodeId → anchoredPosition (基于 pivot=(0,1) 的左上角坐标系)。
    /// </summary>
    public static Dictionary<string, Vector2> ComputeLayout(
        List<TechTreeNodeData> nodes, LayoutConfig config)
    {
        var result = new Dictionary<string, Vector2>();
        if (nodes == null || nodes.Count == 0) return result;

        // 构建 nodeId → info 映射
        var infoMap = new Dictionary<string, NodeLayoutInfo>();
        foreach (var node in nodes)
        {
            if (node == null) continue;
            infoMap[node.NodeId] = new NodeLayoutInfo { node = node };
        }

        // ===== Step 1: 层级分配 =====
        AssignLayers(infoMap);

        // 按层分组
        var layers = BuildLayerLists(infoMap);

        // ===== Step 2: 减少交叉 (重心法) =====
        ReduceCrossings(layers, infoMap, config.barycenterIterations);

        // ===== Step 3: 坐标分配 =====
        AssignCoordinates(layers, config, result);

        return result;
    }

    /// <summary>获取布局后的 Content 尺寸</summary>
    public static Vector2 GetContentSize(
        Dictionary<string, Vector2> positions, LayoutConfig config)
    {
        if (positions == null || positions.Count == 0)
            return new Vector2(1000, 800);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var pos in positions.Values)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }

        // 加上节点本身宽高和边距
        float width = maxX - minX + config.nodeWidth + config.originX;
        float height = (maxY - minY) + config.nodeHeight + config.originY;

        return new Vector2(
            Mathf.Max(width, 1000),
            Mathf.Max(height, 800)
        );
    }

    // ===== Step 1: 层级分配 =====

    /// <summary>
    /// 层级 = 0 (无前置) 或 Max(所有前置层级) + 1。
    /// 使用 DFS + 记忆化避免重复计算。
    /// </summary>
    private static void AssignLayers(Dictionary<string, NodeLayoutInfo> infoMap)
    {
        var visiting = new HashSet<string>();
        foreach (var kvp in infoMap)
        {
            if (kvp.Value.layer < 0)
                ComputeLayer(kvp.Value.node, infoMap, visiting);
        }
    }

    /// <summary>递归计算层级（记忆化）</summary>
    private static int ComputeLayer(
        TechTreeNodeData node,
        Dictionary<string, NodeLayoutInfo> infoMap,
        HashSet<string> visiting)
    {
        if (node == null) return 0;
        if (!infoMap.TryGetValue(node.NodeId, out var info))
            return 0;

        // 已计算过
        if (info.layer >= 0) return info.layer;

        // 环检测
        if (visiting.Contains(node.NodeId))
            return 0;
        visiting.Add(node.NodeId);

        int maxPrevLayer = -1;
        if (node.PreviousNodes != null)
        {
            foreach (var prev in node.PreviousNodes)
            {
                if (prev == null) continue;
                int prevLayer = ComputeLayer(prev, infoMap, visiting);
                if (prevLayer > maxPrevLayer)
                    maxPrevLayer = prevLayer;
            }
        }

        visiting.Remove(node.NodeId);

        int layer = (maxPrevLayer >= 0) ? maxPrevLayer + 1 : 0;
        info.layer = layer;
        return layer;
    }

    /// <summary>按层分组，返回 List[List[NodeLayoutInfo]]</summary>
    private static List<List<NodeLayoutInfo>> BuildLayerLists(
        Dictionary<string, NodeLayoutInfo> infoMap)
    {
        int maxLayer = 0;
        foreach (var kvp in infoMap)
        {
            if (kvp.Value.layer > maxLayer)
                maxLayer = kvp.Value.layer;
        }

        var layers = new List<List<NodeLayoutInfo>>(maxLayer + 1);
        for (int i = 0; i <= maxLayer; i++)
            layers.Add(new List<NodeLayoutInfo>());

        foreach (var kvp in infoMap)
        {
            int l = Mathf.Max(0, kvp.Value.layer);
            if (l < layers.Count)
                layers[l].Add(kvp.Value);
        }

        return layers;
    }

    // ===== Step 2: 减少交叉 (重心法) =====

    /// <summary>
    /// 交替 Top-Down 和 Bottom-Up 重心排序，迭代 iterations 次。
    /// Top-Down: 按"前置节点在上一层的索引平均值"排序。
    /// Bottom-Up: 按"后继节点在下一层的索引平均值"排序。
    /// </summary>
    private static void ReduceCrossings(
        List<List<NodeLayoutInfo>> layers,
        Dictionary<string, NodeLayoutInfo> infoMap,
        int iterations)
    {
        if (layers.Count <= 1) return;

        // 初始 indexInLayer
        UpdateIndices(layers);

        for (int iter = 0; iter < iterations; iter++)
        {
            // Top-Down: layer 1 → max
            for (int l = 1; l < layers.Count; l++)
            {
                SortLayerByPrevBarycenter(layers[l], infoMap);
            }
            UpdateIndices(layers);

            // Bottom-Up: max-1 → 0
            for (int l = layers.Count - 2; l >= 0; l--)
            {
                SortLayerByNextBarycenter(layers[l], infoMap);
            }
            UpdateIndices(layers);
        }
    }

    /// <summary>按前置节点的 Y 坐标平均值（重心）排序当前层</summary>
    private static void SortLayerByPrevBarycenter(
        List<NodeLayoutInfo> layerNodes,
        Dictionary<string, NodeLayoutInfo> infoMap)
    {
        foreach (var info in layerNodes)
        {
            if (info.node.PreviousNodes == null || info.node.PreviousNodes.Length == 0)
            {
                info.barycenter = info.indexInLayer;
                continue;
            }

            float sum = 0;
            int count = 0;
            foreach (var prev in info.node.PreviousNodes)
            {
                if (prev == null) continue;
                if (infoMap.TryGetValue(prev.NodeId, out var prevInfo))
                {
                    sum += prevInfo.indexInLayer;
                    count++;
                }
            }
            info.barycenter = count > 0 ? sum / count : info.indexInLayer;
        }

        layerNodes.Sort((a, b) => a.barycenter.CompareTo(b.barycenter));
    }

    /// <summary>按后继节点的 Y 坐标平均值（重心）排序当前层</summary>
    private static void SortLayerByNextBarycenter(
        List<NodeLayoutInfo> layerNodes,
        Dictionary<string, NodeLayoutInfo> infoMap)
    {
        foreach (var info in layerNodes)
        {
            if (info.node.NextNodes == null || info.node.NextNodes.Length == 0)
            {
                info.barycenter = info.indexInLayer;
                continue;
            }

            float sum = 0;
            int count = 0;
            foreach (var next in info.node.NextNodes)
            {
                if (next == null) continue;
                if (infoMap.TryGetValue(next.NodeId, out var nextInfo))
                {
                    sum += nextInfo.indexInLayer;
                    count++;
                }
            }
            info.barycenter = count > 0 ? sum / count : info.indexInLayer;
        }

        layerNodes.Sort((a, b) => a.barycenter.CompareTo(b.barycenter));
    }

    // ===== Step 3: 坐标分配 =====

    /// <summary>
    /// 节点 Y 坐标跟随前置节点的 Y 坐标（连续坐标法），减少连线斜率。
    /// 逐层从上到下处理，层内按重心排序后的顺序分配:
    ///   - 理想 Y = 前置节点 Y 的平均值
    ///   - 不能与同层上方节点重叠（至少保持 nodeSpacingY 间距）
    ///   - 根节点层均匀分布
    /// 然后做紧凑化: 仅消除因约束下推造成的过大空隙，不拉回上方。
    /// </summary>
    private static void AssignCoordinates(
        List<List<NodeLayoutInfo>> layers,
        LayoutConfig config,
        Dictionary<string, Vector2> result)
    {
        float spacingY = config.nodeSpacingY;
        var idealYMap = new Dictionary<string, float>();

        for (int l = 0; l < layers.Count; l++)
        {
            var layer = layers[l];
            for (int i = 0; i < layer.Count; i++)
            {
                var info = layer[i];
                info.indexInLayer = i;

                float x = config.originX + l * config.layerSpacingX;

                // 理想 Y: 前置节点 Y 的平均值
                float idealY = -(config.originY + i * spacingY); // 默认均匀分布

                if (info.node.PreviousNodes != null && info.node.PreviousNodes.Length > 0)
                {
                    float sum = 0;
                    int count = 0;
                    foreach (var prev in info.node.PreviousNodes)
                    {
                        if (prev == null) continue;
                        if (result.TryGetValue(prev.NodeId, out var prevPos))
                        {
                            sum += prevPos.y;
                            count++;
                        }
                    }
                    if (count > 0)
                        idealY = sum / count;
                }

                idealYMap[info.node.NodeId] = idealY;

                // 约束: 不能与同层上方节点重叠
                // Y 向下为负，上方节点 Y 更大，本节点 Y 必须 <= 上方 Y - spacingY
                float actualY = idealY;
                if (i > 0 && result.TryGetValue(layer[i - 1].node.NodeId, out var abovePos))
                {
                    float maxY = abovePos.y - spacingY;
                    if (actualY > maxY)
                        actualY = maxY;
                }

                result[info.node.NodeId] = new Vector2(x, actualY);
            }

            // 紧凑化: 从上往下拉近下方节点（仅当上方节点比理想 Y 更靠上时）
            for (int i = 1; i < layer.Count; i++)
            {
                if (!result.TryGetValue(layer[i].node.NodeId, out var pos) ||
                    !result.TryGetValue(layer[i - 1].node.NodeId, out var abovePos)) continue;

                float ideal = idealYMap[layer[i].node.NodeId];
                float maxAllowed = abovePos.y - spacingY;

                // 目标 Y = min(ideal, maxAllowed) — 跟随理想位置但不与上方重叠
                float targetY = Mathf.Min(ideal, maxAllowed);

                // 仅当当前 Y 比目标 Y 更靠下（更负）时才拉近
                if (pos.y < targetY)
                {
                    result[layer[i].node.NodeId] = new Vector2(pos.x, targetY);
                }
            }
        }
    }

    // ===== 辅助 =====

    private static void UpdateIndices(List<List<NodeLayoutInfo>> layers)
    {
        foreach (var layer in layers)
        {
            for (int i = 0; i < layer.Count; i++)
                layer[i].indexInLayer = i;
        }
    }
}
