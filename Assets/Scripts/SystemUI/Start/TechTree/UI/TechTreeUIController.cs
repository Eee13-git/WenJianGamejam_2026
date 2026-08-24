using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 科技树 UI 控制器 (MVC Controller) — 类似 SkillUIController。
/// 面板框架来自 TechTreePanel.prefab（静态），节点和连线动态生成：
/// 从 TechTreeConfig 的根节点遍历，实例化 TechTreeNodeView 预制体。
/// 构建 TechTreeNodeViewData 推给 View，处理节点解锁和连线刷新。
///
/// 使用方式:
///   TechTreeUIController.Show()  — 打开面板
///   TechTreeUIController.Hide()  — 关闭面板
/// </summary>
public class TechTreeUIController : MonoBehaviour
{
    // ========== 布局常量 ==========

    private const float NODE_WIDTH = 160f;
    private const float NODE_HEIGHT = 100f;
    private const int BEZIER_SEGMENTS = 12;

    // ========== 布局配置 ==========

    [Header("自动布局参数")]
    [SerializeField] private TechTreeAutoLayout.LayoutConfig _layoutConfig =
        TechTreeAutoLayout.LayoutConfig.Default;

    // ========== 颜色 ==========

    private static readonly Color PanelBgColor = new(0.1f, 0.1f, 0.15f, 0.95f);
    private static readonly Color HeaderColor = new(0.15f, 0.15f, 0.2f, 1f);
    private static readonly Color LineColor = new(0.5f, 0.5f, 0.6f, 0.8f);
    private static readonly Color ActiveLineColor = new(0.2f, 0.9f, 0.3f, 0.9f);
    private static readonly Color TitleColor = new(1f, 0.85f, 0.2f);
    private static readonly Color InfoColor = new(0.85f, 0.85f, 0.9f);

    // ========== UI 引用（由 TechTreePanel.prefab 序列化绑定） ==========

    [SerializeField] private TMP_Text _techPointsText;
    [SerializeField] private TMP_Text _hintText;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _contentTransform;
    [SerializeField] private GameObject _lineLayer;
    [SerializeField] private TechTreeNodeView _nodeViewPrefab;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _backdropButton;

    // ========== 运行时状态 ==========

    private readonly Dictionary<string, TechTreeNodeView> _nodeViews = new();
    private Dictionary<string, Vector2> _nodePositions = new();

    // ========== 单例（场景级） ==========

    private static TechTreeUIController _instance;
    private static TechTreeUIController Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindObjectOfType<TechTreeUIController>(true);
            return _instance;
        }
    }

    // ========== 公开接口 ==========

    public static void Show()
    {
        var inst = Instance;
        if (inst == null) return;
        inst.gameObject.SetActive(true);
        inst.Refresh();
        // 垂直居中显示
        if (inst._scrollRect != null)
            inst._scrollRect.verticalNormalizedPosition = 0.5f;
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.gameObject.SetActive(false);
    }

    public static void Toggle()
    {
        var inst = Instance;
        if (inst == null) return;
        if (inst.gameObject.activeSelf)
            Hide();
        else
            Show();
    }

    // ========== 初始化 ==========

    private void Awake()
    {
        _instance = this;

        // 节点 prefab 未拖引用时回退 Resources.Load
        if (_nodeViewPrefab == null)
            _nodeViewPrefab = Resources.Load<TechTreeNodeView>("TechTree/TechTreeNodeView");

        // 关闭事件绑定（prefab 无法序列化运行时 lambda，统一在此绑定）
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
        if (_backdropButton != null)
            _backdropButton.onClick.AddListener(Hide);

        // 布局 + 动态生成节点与连线
        ComputeLayout();
        if (_lineLayer != null && _contentTransform != null)
            _lineLayer.GetComponent<RectTransform>().sizeDelta = _contentTransform.sizeDelta;
        CreateNodes(_contentTransform);
        if (_lineLayer != null)
            CreateLines(_lineLayer.transform);

        if (TechTreeManager.Instance != null)
        {
            TechTreeManager.Instance.OnTechPointsChanged += _ => Refresh();
            TechTreeManager.Instance.OnNodeUnlocked += _ => Refresh();
        }
    }

    private void OnDestroy()
    {
        if (TechTreeManager.Instance != null)
        {
            TechTreeManager.Instance.OnTechPointsChanged -= _ => Refresh();
            TechTreeManager.Instance.OnNodeUnlocked -= _ => Refresh();
        }
    }

    // ========== 布局计算 ==========

    /// <summary>计算所有节点位置。
    /// 优先使用节点 Position(列,行) 字段的手动网格布局；
    /// 当所有节点 Position 均为 (0,0) 时，回退到 DAG 自动布局。</summary>
    private void ComputeLayout()
    {
        var config = TechTreeManager.Instance?.Config;
        if (config == null) return;

        var allNodes = config.GetAllNodes();
        bool useManual = HasManualPositions(allNodes);

        if (useManual)
            ComputeManualLayout(allNodes);
        else
            ComputeAutoLayout(allNodes);
    }

    /// <summary>检查是否有节点配置了手动位置 (Position != Vector2.zero)</summary>
    private static bool HasManualPositions(List<TechTreeNodeData> nodes)
    {
        foreach (var node in nodes)
        {
            if (node != null && node.Position != Vector2.zero)
                return true;
        }
        return false;
    }

    /// <summary>Manual grid layout: node.Position.(x,y) = (column, row), 0-indexed</summary>
    private void ComputeManualLayout(List<TechTreeNodeData> nodes)
    {
        _nodePositions = new Dictionary<string, Vector2>();
        float maxX = 0f, maxY = 0f;

        // 先计算节点 Y 范围，用于居中偏移
        float minNodeY = float.MaxValue, maxNodeY = float.MinValue;
        foreach (var node in nodes)
        {
            if (node == null) continue;
            float y = -(_layoutConfig.originY + node.Position.y * _layoutConfig.nodeSpacingY);
            if (y < minNodeY) minNodeY = y;
            if (y > maxNodeY) maxNodeY = y;
        }
        // 节点群高度 = maxY - minY（正值），视口高度约 800
        float nodeGroupHeight = maxNodeY - minNodeY + _layoutConfig.nodeHeight;
        float viewportHeight = 800f;
        // 居中偏移：如果节点群比视口矮，向下偏移让它在中间
        float centerYOffset = Mathf.Max(0f, (viewportHeight - nodeGroupHeight) * 0.5f);

        foreach (var node in nodes)
        {
            if (node == null) continue;

            float x = _layoutConfig.originX + node.Position.x * _layoutConfig.layerSpacingX;
            float y = -(_layoutConfig.originY + node.Position.y * _layoutConfig.nodeSpacingY) - centerYOffset;

            _nodePositions[node.NodeId] = new Vector2(x, y);

            if (x > maxX) maxX = x;
            if (node.Position.y > maxY) maxY = node.Position.y;
        }

        if (_contentTransform != null)
        {
            float width = maxX + _layoutConfig.nodeWidth + _layoutConfig.originX;
            float height = (maxY + 1) * _layoutConfig.nodeSpacingY + _layoutConfig.originY * 2 + centerYOffset;
            _contentTransform.sizeDelta = new Vector2(
                Mathf.Max(width, 1000),
                Mathf.Max(height, 800)
            );
        }
    }

    /// <summary>使用 DAG 自动布局算法计算所有节点位置</summary>
    private void ComputeAutoLayout(List<TechTreeNodeData> allNodes)
    {
        _nodePositions = TechTreeAutoLayout.ComputeLayout(allNodes, _layoutConfig);

        // 动态设置 Content 尺寸
        if (_contentTransform != null)
        {
            var size = TechTreeAutoLayout.GetContentSize(_nodePositions, _layoutConfig);
            _contentTransform.sizeDelta = size;
        }
    }

    /// <summary>从根节点 BFS 遍历，实例化预制体（位置由自动布局计算）</summary>
    private void CreateNodes(Transform contentParent)
    {
        var config = TechTreeManager.Instance?.Config;
        if (config == null || config.RootNodes == null) return;

        var allNodes = config.GetAllNodes();
        foreach (var node in allNodes)
        {
            var view = InstantiateNodeView(contentParent, node);
            if (view != null)
            {
                _nodeViews[node.NodeId] = view;
            }
        }
    }

    /// <summary>实例化单个节点视图</summary>
    private TechTreeNodeView InstantiateNodeView(Transform parent, TechTreeNodeData node)
    {
        TechTreeNodeView view;
        if (_nodeViewPrefab != null)
        {
            var go = Object.Instantiate(_nodeViewPrefab.gameObject, parent, false);
            go.name = $"Node_{node.NodeId}";
            go.layer = 5;
            view = go.GetComponent<TechTreeNodeView>();
        }
        else
        {
            // 无预制体时代码构建
            var go = new GameObject($"Node_{node.NodeId}");
            go.transform.SetParent(parent, false);
            go.layer = 5;
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(NODE_WIDTH, NODE_HEIGHT);
            rt.pivot = new Vector2(0.5f, 0.5f);
            view = go.AddComponent<TechTreeNodeView>();
        }

        var rt2 = view.GetComponent<RectTransform>();
        rt2.anchorMin = new Vector2(0, 1);
        rt2.anchorMax = new Vector2(0, 1);
        rt2.sizeDelta = new Vector2(NODE_WIDTH, NODE_HEIGHT);
        rt2.pivot = new Vector2(0.5f, 0.5f);
        rt2.anchoredPosition = _nodePositions.GetValueOrDefault(node.NodeId, Vector2.zero);

        return view;
    }

    /// <summary>创建贝塞尔曲线连线</summary>
    private void CreateLines(Transform lineParent)
    {
        var config = TechTreeManager.Instance?.Config;
        if (config == null || config.RootNodes == null) return;

        var allNodes = config.GetAllNodes();
        foreach (var node in allNodes)
        {
            if (node.NextNodes == null) continue;
            foreach (var nextNode in node.NextNodes)
            {
                if (nextNode == null) continue;
                if (!_nodePositions.TryGetValue(node.NodeId, out var start) ||
                    !_nodePositions.TryGetValue(nextNode.NodeId, out var end))
                    continue;

                bool unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked(node.NodeId);
                CreateBezierLine(lineParent, start, end, node.NodeId, unlocked);
            }
        }
    }

    /// <summary>创建贝塞尔曲线连线（多段 Image 模拟）</summary>
    private void CreateBezierLine(Transform parent, Vector2 start, Vector2 end, string fromNodeId, bool unlocked)
    {
        // 从节点中心到目标节点中心
        var from = start;
        var to = end;

        // 贝塞尔控制点：水平延伸
        float dx = to.x - from.x;
        var cp1 = new Vector2(from.x + dx * 0.5f, from.y);
        var cp2 = new Vector2(to.x - dx * 0.5f, to.y);

        Color lineColor = unlocked ? ActiveLineColor : LineColor;

        Vector2 prev = from;
        for (int i = 1; i <= BEZIER_SEGMENTS; i++)
        {
            float t = (float)i / BEZIER_SEGMENTS;
            var curr = BezierPoint(from, cp1, cp2, to, t);
            CreateLineSegment(parent, prev, curr, lineColor);
            prev = curr;
        }
    }

    /// <summary>三次贝塞尔曲线点</summary>
    private static Vector2 BezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    /// <summary>创建单段直线</summary>
    private void CreateLineSegment(Transform parent, Vector2 from, Vector2 to, Color color)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);
        go.layer = 5;

        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;

        Vector2 delta = to - from;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(length, 4f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = from;
        rt.localEulerAngles = new Vector3(0, 0, angle);
    }

    // ========== 刷新 ==========

    private void Refresh()
    {
        var mgr = TechTreeManager.Instance;
        if (mgr == null) return;

        // 科技点显示
        if (_techPointsText != null)
            _techPointsText.text = $"科技点: {mgr.TechPoints}";

        // 提示文本
        if (_hintText != null)
        {
            int unlockedCount = 0;
            int totalCount = 0;
            if (mgr.Config != null)
            {
                var all = mgr.Config.GetAllNodes();
                totalCount = all.Count;
                foreach (var node in all)
                {
                    if (mgr.IsUnlocked(node.NodeId)) unlockedCount++;
                }
            }
            _hintText.text = $"已解锁: {unlockedCount}/{totalCount}  |  点击节点解锁，获得属性加成";
        }

        // 刷新节点视图
        foreach (var kvp in _nodeViews)
        {
            var node = mgr.Config?.GetNodeById(kvp.Key);
            if (node == null) continue;

            var data = new TechTreeNodeViewData
            {
                NodeId = node.NodeId,
                DisplayName = node.DisplayName,
                Description = node.Description,
                EffectSummary = node.Effect != null ? node.Effect.GetEffectSummary() : "",
                Cost = node.Cost,
                IsUnlocked = mgr.IsUnlocked(node.NodeId),
                CanUnlock = mgr.CanUnlock(node.NodeId),
                CurrentTechPoints = mgr.TechPoints,
                Type = node.Effect is TechTreeMechanismEffect ? TechTreeNodeViewData.NodeType.Mechanism
                     : node.Effect is TechTreeStatMechanismEffect ? TechTreeNodeViewData.NodeType.StatMechanism
                     : TechTreeNodeViewData.NodeType.Normal
            };
            kvp.Value.Configure(data, OnNodeClicked);
            kvp.Value.Refresh(in data);
        }

        // 刷新连线
        RefreshLines();
    }

    private void RefreshLines()
    {
        if (_lineLayer == null) return;

        foreach (Transform child in _lineLayer.transform)
        {
            Destroy(child.gameObject);
        }
        CreateLines(_lineLayer.transform);
    }

    // ========== 事件处理 ==========

    private void OnNodeClicked(string nodeId)
    {
        var mgr = TechTreeManager.Instance;
        if (mgr == null) return;

        if (mgr.CanUnlock(nodeId))
        {
            mgr.Unlock(nodeId);
        }
    }
}
