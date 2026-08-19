using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 寻路网格 — 运行时构建，存储在 RoomRoot 中供敌人共享。
/// 使用虚拟矩形栅格化检测障碍物（Rect.Overlaps vs Collider.bounds）。
/// </summary>
public class PathfindingGrid
{
    private readonly bool[,] _walkable;
    private readonly Vector2 _origin;
    private readonly float _cellSize;
    private readonly int _width;
    private readonly int _height;

    public int Width => _width;
    public int Height => _height;
    public Vector2 Origin => _origin;
    public float CellSize => _cellSize;

    public PathfindingGrid(Vector2 origin, int width, int height, float cellSize)
    {
        _origin = origin;
        _width = width;
        _height = height;
        _cellSize = cellSize;
        _walkable = new bool[width, height];
    }

    /// <summary>
    /// 虚拟矩形栅格化构建：
    /// 1. 收集房间范围内所有 Wall/Obstacles 的 Collider.bounds
    /// 2. 每个 cell 的 Rect（收缩 shrink）与障碍物 bounds 做交集检测
    /// </summary>
    public static PathfindingGrid Build(Vector2 center, Vector2 roomSize, float cellSize = 1f)
    {
        int width = Mathf.RoundToInt(roomSize.x / cellSize);
        int height = Mathf.RoundToInt(roomSize.y / cellSize);
        Vector2 origin = center - new Vector2(width * cellSize * 0.5f, height * cellSize * 0.5f);

        var grid = new PathfindingGrid(origin, width, height, cellSize);

        // 1. 收集房间范围内所有 Wall/Obstacles 的 bounds
        Vector2 bottomLeft = origin;
        Vector2 topRight = origin + new Vector2(width * cellSize, height * cellSize);
        var colliders = Physics2D.OverlapAreaAll(bottomLeft, topRight);

        var obstacleBounds = new List<Rect>();
        foreach (var col in colliders)
        {
            if (col.CompareTag("Wall") || col.CompareTag("Obstacles"))
            {
                var b = col.bounds;
                obstacleBounds.Add(new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
            }
        }

        // 2. 每个 cell 的收缩 Rect 与障碍物 bounds 交集检测
        float shrink = cellSize * 0.1f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 cellCenter = grid.CellToWorld(x, y);
                Rect cellRect = new Rect(
                    cellCenter.x - cellSize * 0.5f + shrink,
                    cellCenter.y - cellSize * 0.5f + shrink,
                    cellSize - shrink * 2f,
                    cellSize - shrink * 2f);

                bool blocked = false;
                foreach (var obsRect in obstacleBounds)
                {
                    if (cellRect.Overlaps(obsRect))
                    {
                        blocked = true;
                        break;
                    }
                }
                grid._walkable[x, y] = !blocked;
            }
        }
        return grid;
    }

    public bool IsWalkable(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        return _walkable[x, y];
    }

    public bool InBounds(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;

    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        Vector2 local = worldPos - _origin;
        return new Vector2Int(
            Mathf.FloorToInt(local.x / _cellSize),
            Mathf.FloorToInt(local.y / _cellSize));
    }

    public Vector2 CellToWorld(int x, int y)
    {
        return _origin + new Vector2(
            (x + 0.5f) * _cellSize,
            (y + 0.5f) * _cellSize);
    }

    public Vector2Int ClampToGrid(Vector2 worldPos)
    {
        var cell = WorldToCell(worldPos);
        cell.x = Mathf.Clamp(cell.x, 0, _width - 1);
        cell.y = Mathf.Clamp(cell.y, 0, _height - 1);
        return cell;
    }

    /// <summary>找最近的可通行 cell（螺旋搜索）</summary>
    public Vector2Int FindNearestWalkable(Vector2Int center)
    {
        if (IsWalkable(center.x, center.y)) return center;
        for (int r = 1; r < 10; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = center.x + dx;
                    int y = center.y + dy;
                    if (InBounds(x, y) && IsWalkable(x, y))
                        return new Vector2Int(x, y);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }
}
