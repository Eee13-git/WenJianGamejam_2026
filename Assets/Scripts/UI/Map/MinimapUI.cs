using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小地图 UI — 在界面右上角显示房间和走廊的关系图。
/// 从 MapManager.RoomGraph 读取数据，绘制到 RawImage 的 Texture2D 上。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class MinimapUI : MonoBehaviour
{
    [Header("小地图尺寸")]
    [SerializeField] private int _textureSize = 256;
    [SerializeField] private float _uiWidth = 200f;
    [SerializeField] private float _uiHeight = 200f;

    [Header("绘制参数")]
    [SerializeField] private float _worldPadding = 2f;
    [SerializeField] private int _corridorWidth = 4;
    [SerializeField] [Range(0.2f, 1f)] private float _roomDisplayScale = 0.55f;

    [Header("房间颜色")]
    [SerializeField] private Color _startColor = new(0.3f, 0.8f, 0.3f);
    [SerializeField] private Color _normalColor = new(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color _treasureColor = new(1f, 0.84f, 0f);
    [SerializeField] private Color _bossColor = new(0.8f, 0.2f, 0.2f);
    [SerializeField] private Color _shopColor = new(0.3f, 0.6f, 1f);
    [SerializeField] private Color _exitColor = new(0.6f, 0.2f, 0.8f);
    [SerializeField] private Color _hiddenColor = new(0.85f, 0.5f, 0.1f);
    [SerializeField] private Color _explorableColor = new(0.25f, 0.25f, 0.25f, 0.45f);
    [SerializeField] private Color _explorableBorderColor = new(0.18f, 0.18f, 0.18f, 0.55f);
    [SerializeField] private Color _explorableCorridorColor = new(0.18f, 0.18f, 0.18f, 0.35f);
    [SerializeField] private Color _currentBorderColor = Color.white;
    [SerializeField] private Color _corridorColor = new(0.28f, 0.28f, 0.28f, 0.7f);
    [SerializeField] private Color _bgColor = new(0f, 0f, 0f, 0.6f);

    private RawImage _rawImage;
    private Texture2D _texture;
    private int _currentRoomId = -1;
    private System.Collections.Generic.HashSet<int> _visitedRoomIds = new();

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        if (_rawImage == null)
            _rawImage = gameObject.AddComponent<RawImage>();

        ResizeUI();
        CreateTexture();
    }

    private void Start()
    {
        // 订阅房间切换事件
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchCompleted += OnRoomSwitchCompleted;

        // 尝试立即绘制；如果地图还没生成，延迟一帧再试
        TryDraw();

        if (_currentRoomId < 0)
            StartCoroutine(DelayedDraw());
    }

    private IEnumerator DelayedDraw()
    {
        yield return null;
        TryDraw();
    }

    private void OnDestroy()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchCompleted -= OnRoomSwitchCompleted;

        if (_texture != null)
            Destroy(_texture);
    }

    private void OnRoomSwitchCompleted(int newRoomId)
    {
        _visitedRoomIds.Add(newRoomId);
        _currentRoomId = newRoomId;
        var graph = MapManager.Instance?.RoomGraph;
        if (graph != null)
            DrawMap(graph);
    }

    // ==================== 公开方法 ====================

    [ContextMenu("Refresh")]
    public void Refresh()
    {
        var mgr = MapManager.Instance;
        if (mgr == null) return;
        var graph = mgr.RoomGraph;
        if (graph == null) return;
        _currentRoomId = mgr.CurrentRoomId;
        _visitedRoomIds.Add(_currentRoomId);
        DrawMap(graph);
    }

    // ==================== 内部绘制 ====================

    private void ResizeUI()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _uiWidth);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _uiHeight);
    }

    private void CreateTexture()
    {
        _texture = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
        _texture.filterMode = FilterMode.Point;
        _texture.wrapMode = TextureWrapMode.Clamp;
        _rawImage.texture = _texture;
    }

    private void TryDraw()
    {
        var mgr = MapManager.Instance;
        if (mgr == null) return;
        var graph = mgr.RoomGraph;
        if (graph == null || graph.nodes.Count == 0) return;

        _currentRoomId = mgr.CurrentRoomId;
        _visitedRoomIds.Add(_currentRoomId);
        DrawMap(graph);
    }

    private void DrawMap(RoomGraph graph)
    {
        if (_texture == null) CreateTexture();
        if (graph == null || graph.nodes.Count == 0) return;

        // 1. 计算世界边界
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var node in graph.nodes)
        {
            Vector2 half = node.config.roomSize * 0.5f;
            minX = Mathf.Min(minX, node.worldPosition.x - half.x);
            maxX = Mathf.Max(maxX, node.worldPosition.x + half.x);
            minY = Mathf.Min(minY, node.worldPosition.y - half.y);
            maxY = Mathf.Max(maxY, node.worldPosition.y + half.y);
        }

        float worldW = maxX - minX;
        float worldH = maxY - minY;
        if (worldW < 0.001f || worldH < 0.001f) return;

        // 2. 计算缩放比例 (保持宽高比，并预留边距)
        float texPadding = _textureSize * 0.06f;
        float usableSize = _textureSize - texPadding * 2f;
        float scale = Mathf.Min(usableSize / worldW, usableSize / worldH);
        float offsetX = (usableSize - worldW * scale) / 2f;
        float offsetY = (usableSize - worldH * scale) / 2f;

        // 世界坐标 -> 像素坐标
        int WorldToPixelX(float wx) => Mathf.RoundToInt(texPadding + offsetX + (wx - minX) * scale);
        int WorldToPixelY(float wy) => Mathf.RoundToInt(texPadding + offsetY + (wy - minY) * scale);

        // 3. 清空纹理
        Color[] pixels = new Color[_textureSize * _textureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = _bgColor;

        // 4. 计算"可探索"房间: 未访问但与已探索房间相邻的房间 (隐藏房除外)
        var explorableIds = new System.Collections.Generic.HashSet<int>();
        foreach (var node in graph.nodes)
        {
            if (_visitedRoomIds.Contains(node.roomId)) continue;
            if (node.roomType == RoomType.Hidden) continue;
            foreach (var conn in node.connections)
            {
                if (_visitedRoomIds.Contains(conn.Key))
                {
                    explorableIds.Add(node.roomId);
                    break;
                }
            }
        }

        // 5. 画走廊 (隐藏房不参与可探索连线)
        foreach (var node in graph.nodes)
        {
            foreach (var conn in node.connections)
            {
                if (node.roomId >= conn.Key) continue;
                var neighbor = graph.GetNode(conn.Key);
                if (neighbor == null) continue;

                // 隐藏房不显示在可探索状态
                if (!_visitedRoomIds.Contains(node.roomId) && node.roomType == RoomType.Hidden) continue;
                if (!_visitedRoomIds.Contains(neighbor.roomId) && neighbor.roomType == RoomType.Hidden) continue;

                bool aVisited = _visitedRoomIds.Contains(node.roomId);
                bool bVisited = _visitedRoomIds.Contains(neighbor.roomId);
                bool aExplorable = explorableIds.Contains(node.roomId);
                bool bExplorable = explorableIds.Contains(neighbor.roomId);

                bool draw = (aVisited || bVisited) && (aVisited || bVisited || aExplorable || bExplorable);
                if (!draw) continue;

                Color lineColor = (aVisited && bVisited) ? _corridorColor : _explorableCorridorColor;

                Vector2 centerA = node.worldPosition;
                Vector2 centerB = neighbor.worldPosition;
                DrawThickLine(pixels,
                    WorldToPixelX(centerA.x), WorldToPixelY(centerA.y),
                    WorldToPixelX(centerB.x), WorldToPixelY(centerB.y),
                    lineColor, _corridorWidth);
            }
        }

        // 6. 画可探索房间: Boss/Exit 用真实颜色，其余用灰色
        foreach (var node in graph.nodes)
        {
            if (!explorableIds.Contains(node.roomId)) continue;

            bool isBossOrExit = node.roomType == RoomType.Boss || node.roomType == RoomType.Exit;
            Color fillColor = isBossOrExit ? GetRoomColor(node.roomType) : _explorableColor;
            Color borderColor = isBossOrExit
                ? Color.Lerp(GetRoomColor(node.roomType), Color.black, 0.3f)
                : _explorableBorderColor;

            Vector2 half = node.config.roomSize * 0.5f * _roomDisplayScale;
            int px1 = ClampPixel(WorldToPixelX(node.worldPosition.x - half.x));
            int py1 = ClampPixel(WorldToPixelY(node.worldPosition.y - half.y));
            int px2 = ClampPixel(WorldToPixelX(node.worldPosition.x + half.x));
            int py2 = ClampPixel(WorldToPixelY(node.worldPosition.y + half.y));

            for (int y = py1; y <= py2; y++)
            {
                for (int x = px1; x <= px2; x++)
                {
                    bool isBorder = (x == px1 || x == px2 || y == py1 || y == py2);
                    pixels[y * _textureSize + x] = isBorder ? borderColor : fillColor;
                }
            }
        }

        // 7. 画已探索房间
        foreach (var node in graph.nodes)
        {
            if (!_visitedRoomIds.Contains(node.roomId)) continue;

            Color roomColor = GetRoomColor(node.roomType);

            Vector2 half = node.config.roomSize * 0.5f * _roomDisplayScale;
            int px1 = ClampPixel(WorldToPixelX(node.worldPosition.x - half.x));
            int py1 = ClampPixel(WorldToPixelY(node.worldPosition.y - half.y));
            int px2 = ClampPixel(WorldToPixelX(node.worldPosition.x + half.x));
            int py2 = ClampPixel(WorldToPixelY(node.worldPosition.y + half.y));

            for (int y = py1; y <= py2; y++)
            {
                for (int x = px1; x <= px2; x++)
                {
                    bool isBorder = (x == px1 || x == px2 || y == py1 || y == py2);

                    if (isBorder)
                    {
                        pixels[y * _textureSize + x] = Color.Lerp(roomColor, Color.black, 0.3f);
                    }
                    else
                    {
                        pixels[y * _textureSize + x] = roomColor;
                    }
                }
            }

            // 8. 高亮当前房间边框
            if (node.roomId == _currentRoomId)
            {
                for (int y = py1; y <= py2; y++)
                {
                    for (int x = px1; x <= px2; x++)
                    {
                        bool isBorder = (x == px1 || x == px2 || y == py1 || y == py2);
                        if (isBorder)
                            pixels[y * _textureSize + x] = _currentBorderColor;
                    }
                }
            }
        }

        _texture.SetPixels(pixels);
        _texture.Apply();
    }

    private int ClampPixel(int val)
    {
        return Mathf.Clamp(val, 0, _textureSize - 1);
    }

    private void DrawThickLine(Color[] pixels, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        int half = thickness / 2;

        while (true)
        {
            // 画中心点周围的方形
            for (int dy2 = -half; dy2 <= half; dy2++)
            {
                for (int dx2 = -half; dx2 <= half; dx2++)
                {
                    int tx = x0 + dx2;
                    int ty = y0 + dy2;
                    if (tx >= 0 && tx < _textureSize && ty >= 0 && ty < _textureSize)
                        pixels[ty * _textureSize + tx] = color;
                }
            }

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private Color GetRoomColor(RoomType type)
    {
        return type switch
        {
            RoomType.Start => _startColor,
            RoomType.Normal => _normalColor,
            RoomType.Treasure => _treasureColor,
            RoomType.Boss => _bossColor,
            RoomType.Shop => _shopColor,
            RoomType.Exit => _exitColor,
            RoomType.Hidden => _hiddenColor,
            _ => Color.gray
        };
    }
}
