using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 科技树 UI 控制器 (MVC Controller) — 类似 SkillUIController。
/// 从 TechTreeConfig 的根节点 BFS 遍历，实例化 TechTreeNodeView 预制体。
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
    private const float NODE_SPACING_X = 60f;
    private const float NODE_SPACING_Y = 40f;
    private const float TREE_ORIGIN_X = -600f;
    private const float TREE_ORIGIN_Y = 200f;
    private const int BEZIER_SEGMENTS = 12;

    // ========== 颜色 ==========

    private static readonly Color PanelBgColor = new(0.1f, 0.1f, 0.15f, 0.95f);
    private static readonly Color HeaderColor = new(0.15f, 0.15f, 0.2f, 1f);
    private static readonly Color LineColor = new(0.4f, 0.4f, 0.5f, 0.6f);
    private static readonly Color ActiveLineColor = new(0.2f, 0.8f, 0.2f, 0.6f);
    private static readonly Color TitleColor = new(1f, 0.85f, 0.2f);
    private static readonly Color InfoColor = new(0.85f, 0.85f, 0.9f);

    // ========== UI 引用 ==========

    private GameObject _backdrop;
    private GameObject _panel;
    private TMP_Text _techPointsText;
    private TMP_Text _hintText;
    private readonly Dictionary<string, TechTreeNodeView> _nodeViews = new();
    private readonly Dictionary<string, Vector2> _nodePositions = new();
    private GameObject _lineLayer;
    private TechTreeNodeView _nodeViewPrefab;

    // ========== 单例（场景级） ==========

    private static TechTreeUIController _instance;
    private static TechTreeUIController Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindObjectOfType<TechTreeUIController>(true);
            if (_instance == null)
            {
                _instance = CreatePanel();
            }
            return _instance;
        }
    }

    // ========== 公开接口 ==========

    public static void Show()
    {
        var inst = Instance;
        inst.gameObject.SetActive(true);
        inst.Refresh();
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.gameObject.SetActive(false);
    }

    public static void Toggle()
    {
        var inst = Instance;
        if (inst.gameObject.activeSelf)
            Hide();
        else
            Show();
    }

    // ========== 初始化 ==========

    private void Awake()
    {
        _instance = this;
        LoadPrefab();
        BuildUI();
        gameObject.SetActive(false);

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

    /// <summary>加载节点预制体</summary>
    private void LoadPrefab()
    {
        _nodeViewPrefab = Resources.Load<TechTreeNodeView>("TechTree/TechTreeNodeView");
    }

    // ========== UI 构建 ==========

    private static TechTreeUIController CreatePanel()
    {
        Canvas rootCanvas = FindObjectOfType<Canvas>();
        if (rootCanvas == null)
        {
            var canvasGo = new GameObject("TechTreeCanvas");
            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        var go = new GameObject("TechTreePanel");
        go.layer = 5;
        go.AddComponent<RectTransform>();
        go.transform.SetParent(rootCanvas.transform, false);
        var ctrl = go.AddComponent<TechTreeUIController>();
        return ctrl;
    }

    private void BuildUI()
    {
        var rootRt = GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // === 背景遮罩 ===
        _backdrop = CreateChild("Backdrop", transform);
        StretchToParent(_backdrop);
        var backdropImg = _backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0, 0, 0, 0.6f);
        var backdropBtn = _backdrop.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(Hide);

        // === 主面板 ===
        _panel = CreateChild("Panel", transform);
        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.05f, 0.05f);
        panelRt.anchorMax = new Vector2(0.95f, 0.95f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = PanelBgColor;

        // === 标题栏 ===
        var header = CreateChild("Header", _panel.transform);
        var headerRt = header.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0, 1);
        headerRt.anchorMax = new Vector2(1, 1);
        headerRt.pivot = new Vector2(0.5f, 1);
        headerRt.sizeDelta = new Vector2(0, 60);
        headerRt.anchoredPosition = Vector2.zero;
        var headerImg = header.AddComponent<Image>();
        headerImg.color = HeaderColor;
        headerImg.raycastTarget = false;

        // 标题文本
        var titleGo = CreateChild("TitleText", header.transform);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0);
        titleRt.anchorMax = new Vector2(0.7f, 1);
        titleRt.offsetMin = new Vector2(20, 0);
        titleRt.offsetMax = Vector2.zero;
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "科 技 树";
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = TitleColor;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.verticalAlignment = VerticalAlignmentOptions.Middle;
        titleText.raycastTarget = false;

        // 科技点显示
        var pointsGo = CreateChild("TechPointsText", header.transform);
        var pointsRt = pointsGo.GetComponent<RectTransform>();
        pointsRt.anchorMin = new Vector2(0.7f, 0);
        pointsRt.anchorMax = new Vector2(1, 1);
        pointsRt.offsetMin = Vector2.zero;
        pointsRt.offsetMax = new Vector2(-70, 0);
        _techPointsText = pointsGo.AddComponent<TextMeshProUGUI>();
        _techPointsText.fontSize = 22;
        _techPointsText.fontStyle = FontStyles.Bold;
        _techPointsText.color = new Color(0.4f, 1f, 0.4f);
        _techPointsText.alignment = TextAlignmentOptions.Right;
        _techPointsText.verticalAlignment = VerticalAlignmentOptions.Middle;
        _techPointsText.raycastTarget = false;

        // 关闭按钮
        var closeBtnGo = CreateChild("CloseButton", header.transform);
        var closeRt = closeBtnGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1, 0.5f);
        closeRt.anchorMax = new Vector2(1, 0.5f);
        closeRt.pivot = new Vector2(1, 0.5f);
        closeRt.sizeDelta = new Vector2(50, 50);
        closeRt.anchoredPosition = new Vector2(-10, 0);
        var closeImg = closeBtnGo.AddComponent<Image>();
        closeImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
        var closeBtn = closeBtnGo.AddComponent<Button>();
        var closeColors = closeBtn.colors;
        closeColors.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
        closeBtn.colors = closeColors;
        closeBtn.onClick.AddListener(Hide);
        var closeTextGo = CreateChild("Text", closeBtnGo.transform);
        var closeTextRt = closeTextGo.GetComponent<RectTransform>();
        closeTextRt.anchorMin = Vector2.zero;
        closeTextRt.anchorMax = Vector2.one;
        closeTextRt.offsetMin = Vector2.zero;
        closeTextRt.offsetMax = Vector2.zero;
        var closeText = closeTextGo.AddComponent<TextMeshProUGUI>();
        closeText.text = "×";
        closeText.fontSize = 36;
        closeText.color = Color.white;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.verticalAlignment = VerticalAlignmentOptions.Middle;
        closeText.raycastTarget = false;

        // === 提示文本 ===
        var hintGo = CreateChild("HintText", _panel.transform);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0, 0);
        hintRt.anchorMax = new Vector2(1, 0);
        hintRt.pivot = new Vector2(0.5f, 0);
        hintRt.sizeDelta = new Vector2(0, 30);
        hintRt.anchoredPosition = new Vector2(0, 10);
        _hintText = hintGo.AddComponent<TextMeshProUGUI>();
        _hintText.fontSize = 14;
        _hintText.color = InfoColor;
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.raycastTarget = false;

        // === ScrollView ===
        var scrollGo = CreateChild("NodeScrollView", _panel.transform);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0.05f);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(0, 30);
        scrollRt.offsetMax = Vector2.zero;
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = true;

        var viewportGo = CreateChild("Viewport", scrollGo.transform);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.pivot = new Vector2(0, 1);
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        scrollRect.viewport = viewportRt;

        var contentGo = CreateChild("Content", viewportGo.transform);
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(0, 1);
        contentRt.pivot = new Vector2(0, 1);
        contentRt.sizeDelta = new Vector2(1400, 800);
        contentRt.anchoredPosition = Vector2.zero;
        scrollRect.content = contentRt;

        // 连线层
        _lineLayer = CreateChild("LineLayer", contentGo.transform);
        StretchToParent(_lineLayer);
        _lineLayer.transform.SetAsFirstSibling();

        // 创建节点
        CreateNodes(contentGo.transform);
        // 创建连线
        CreateLines(_lineLayer.transform);
    }

    /// <summary>从根节点 BFS 遍历，实例化预制体</summary>
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
                _nodePositions[node.NodeId] = NodeToAnchoredPos(node.Position);
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
        rt2.sizeDelta = new Vector2(NODE_WIDTH, NODE_HEIGHT);
        rt2.pivot = new Vector2(0.5f, 0.5f);
        rt2.anchoredPosition = NodeToAnchoredPos(node.Position);

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
        // 从节点右边缘到目标左边缘
        var startRight = new Vector2(start.x + NODE_WIDTH / 2f, start.y);
        var endLeft = new Vector2(end.x - NODE_WIDTH / 2f, end.y);

        // 贝塞尔控制点：水平延伸
        float dx = endLeft.x - startRight.x;
        var cp1 = new Vector2(startRight.x + dx * 0.5f, startRight.y);
        var cp2 = new Vector2(endLeft.x - dx * 0.5f, endLeft.y);

        Color lineColor = unlocked ? ActiveLineColor : LineColor;

        Vector2 prev = startRight;
        for (int i = 1; i <= BEZIER_SEGMENTS; i++)
        {
            float t = (float)i / BEZIER_SEGMENTS;
            var curr = BezierPoint(startRight, cp1, cp2, endLeft, t);
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

        rt.sizeDelta = new Vector2(length, 3f);
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
                CurrentTechPoints = mgr.TechPoints
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

    // ========== 辅助 ==========

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = 5;
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchToParent(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Vector2 NodeToAnchoredPos(Vector2 nodePos)
    {
        return new Vector2(
            TREE_ORIGIN_X + nodePos.x * (NODE_WIDTH + NODE_SPACING_X),
            TREE_ORIGIN_Y - nodePos.y * (NODE_HEIGHT + NODE_SPACING_Y)
        );
    }
}
