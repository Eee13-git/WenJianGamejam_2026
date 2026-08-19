using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* 寻路算法 — Octile Distance 启发式，8 方向移动，拐角检查。
/// </summary>
public static class AStarPathfinder
{
    private const float D = 1f;        // 直线移动代价
    private const float D2 = 1.4142f;  // 对角线移动代价 (√2)

    /// <summary>
    /// 返回从 start 到 end 的路径点列表（世界坐标），不含起点，含终点。
    /// 无路径返回 null。
    /// </summary>
    public static List<Vector2> FindPath(Vector2 start, Vector2 end,
        PathfindingGrid grid, int maxNodes = 500)
    {
        var startCell = grid.WorldToCell(start);
        var endCell = grid.WorldToCell(end);

        if (!grid.InBounds(startCell.x, startCell.y))
            startCell = grid.ClampToGrid(start);
        if (!grid.InBounds(endCell.x, endCell.y))
            endCell = grid.ClampToGrid(end);

        if (!grid.IsWalkable(endCell.x, endCell.y))
        {
            endCell = grid.FindNearestWalkable(endCell);
            if (endCell.x < 0) return null;
        }

        var openSet = new List<AStarNode>();
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var fScore = new Dictionary<Vector2Int, float>();

        var startKey = new Vector2Int(startCell.x, startCell.y);
        var endKey = new Vector2Int(endCell.x, endCell.y);

        gScore[startKey] = 0;
        fScore[startKey] = Heuristic(startCell, endCell);
        openSet.Add(new AStarNode(startKey, fScore[startKey]));

        int nodesExplored = 0;

        while (openSet.Count > 0 && nodesExplored < maxNodes)
        {
            // 取 fScore 最小的节点
            openSet.Sort((a, b) => a.F.CompareTo(b.F));
            var current = openSet[0];
            openSet.RemoveAt(0);

            if (current.Cell == endKey)
                return ReconstructPath(cameFrom, current.Cell, grid);

            closedSet.Add(current.Cell);
            nodesExplored++;

            // 8 方向邻居
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = current.Cell.x + dx;
                    int ny = current.Cell.y + dy;
                    var neighbor = new Vector2Int(nx, ny);

                    if (!grid.InBounds(nx, ny)) continue;
                    if (!grid.IsWalkable(nx, ny)) continue;
                    if (closedSet.Contains(neighbor)) continue;

                    // 对角线拐角检查：两个正交邻居必须均可通行
                    if (dx != 0 && dy != 0)
                    {
                        if (!grid.IsWalkable(current.Cell.x + dx, current.Cell.y)) continue;
                        if (!grid.IsWalkable(current.Cell.x, current.Cell.y + dy)) continue;
                    }

                    float moveCost = (dx != 0 && dy != 0) ? D2 : D;
                    float tentativeG = gScore[current.Cell] + moveCost;

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current.Cell;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, endKey);

                        if (!openSet.Exists(n => n.Cell == neighbor))
                            openSet.Add(new AStarNode(neighbor, fScore[neighbor]));
                    }
                }
            }
        }

        return null; // 无路径
    }

    /// <summary>Octile Distance — 匹配 8 方向移动</summary>
    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return D * (dx + dy) + (D2 - 2f * D) * Mathf.Min(dx, dy);
    }

    /// <summary>重建路径 → 世界坐标列表</summary>
    private static List<Vector2> ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current, PathfindingGrid grid)
    {
        var path = new List<Vector2>();
        var cell = current;
        while (cameFrom.ContainsKey(cell))
        {
            path.Add(grid.CellToWorld(cell.x, cell.y));
            cell = cameFrom[cell];
        }
        path.Reverse();
        return path;
    }

    private struct AStarNode
    {
        public Vector2Int Cell;
        public float F;
        public AStarNode(Vector2Int cell, float f) { Cell = cell; F = f; }
    }
}
