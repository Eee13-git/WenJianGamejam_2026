using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapManager : MonoBehaviour
{
    [Header("地图引用")]
    [SerializeField] private Grid grid;                 // 网格
    [SerializeField] private Tilemap groundTilemap;    // 地面层（供视觉参考）
    [SerializeField] private Tilemap obstacleTilemap;  // 障碍物层（墙壁/石头）

    // 单例模式，方便其他脚本直接调用 MapManager.Instance
    public static MapManager Instance { get; private set; }

    // ========== 逻辑网格（二维数组）==========
    // 以 Ground 左上角为 (0,0)，1=有障碍物，0=无障碍物
    private int[,] walkableGrid;
    public int Width { get; private set; }
    public int Height { get; private set; }
    // Ground 左上角对应的 Tilemap 坐标（用于索引 ↔ 坐标转换）
    private Vector3Int gridOrigin;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (grid == null) grid = GetComponent<Grid>();
        if (groundTilemap != null)
            BuildWalkableGrid();
        else
            Debug.LogWarning("MapManager: 未指定 Ground Tilemap，无法构建网格");
    }

    /// <summary> 扫描 Tilemap 构建逻辑网格（以 Ground 左上角为原点） </summary>
    private void BuildWalkableGrid()
    {
        BoundsInt bounds = groundTilemap.cellBounds;
        Width = bounds.size.x;
        Height = bounds.size.y;
        // 左上角格子 = (minX, maxY - 1)
        gridOrigin = new Vector3Int(bounds.min.x, bounds.max.y - 1, 0);

        walkableGrid = new int[Width, Height];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // 网格索引 (x, y) 对应 Tilemap 坐标 (gridOrigin.x + x, gridOrigin.y - y)
                Vector3Int cellPos = new Vector3Int(gridOrigin.x + x, gridOrigin.y - y, 0);
                walkableGrid[x, y] = (obstacleTilemap != null && obstacleTilemap.GetTile(cellPos) != null) ? 1 : 0;
            }
        }
    }

    // ========== 坐标转换 ==========
    // 世界坐标 → 网格索引（0,0 在左上角）
    public Vector2Int WorldToGridIndex(Vector3 worldPos)
    {
        Vector3Int cell = grid.WorldToCell(worldPos);
        return new Vector2Int(cell.x - gridOrigin.x, gridOrigin.y - cell.y);
    }

    // 网格索引 → 世界坐标（格子中心点）
    public Vector3 GridIndexToWorld(Vector2Int gridIndex)
    {
        Vector3Int cell = new Vector3Int(gridOrigin.x + gridIndex.x, gridOrigin.y - gridIndex.y, 0);
        return grid.GetCellCenterWorld(cell);
    }

    /// <summary> 世界坐标 -> 格子坐标（兼容旧代码） </summary>
    public Vector3Int WorldToCell(Vector3 worldPosition) => grid.WorldToCell(worldPosition);

    /// <summary> 格子坐标 -> 世界坐标（格子中心点，兼容旧代码） </summary>
    public Vector3 CellToWorld(Vector3Int cellPosition) => grid.GetCellCenterWorld(cellPosition);

    // ========== 通行检测 ==========
    // 网格索引版本：0=可通行，1=有障碍物
    public bool IsWalkable(Vector2Int gridIndex) => IsWalkable(gridIndex.x, gridIndex.y);

    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
        return walkableGrid[x, y] == 0;
    }

    // 世界坐标版本（兼容 PlayerController）
    public bool IsWalkable(Vector3 worldPos) => IsWalkable(WorldToGridIndex(worldPos));

    // ========== 视线检测 ==========
    /// <summary>
    /// 检查从 from 到 to 间是否有障碍物阻挡（布雷森汉姆直线算法）
    /// 返回 true = 视线畅通（无障碍物）；返回 false = 视线被阻（有障碍物）
    /// </summary>
    /// 
    public bool HasLineOfSight(Vector2Int from, Vector2Int to)
    {
        return HasLineOfSight(from, to, 0f);
    }

    public bool HasLineOfSight(Vector2Int from, Vector2Int to, float radius)
    {
        if (!IsWalkable(from) || !IsWalkable(to)) return false;

        // 将半径转换为格子数（至少为1，保证至少检测中心格子本身）
        int checkRadius = Mathf.Max(1, Mathf.CeilToInt(radius));

        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            // 检查以 (x0, y0) 为中心，边长为 (2*checkRadius+1) 的正方形区域
            bool blocked = false;
            for (int ox = -checkRadius; ox <= checkRadius; ox++)
            {
                for (int oy = -checkRadius; oy <= checkRadius; oy++)
                {
                    int checkX = x0 + ox;
                    int checkY = y0 + oy;
                    if (!IsWalkable(checkX, checkY))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) break;
            }
            if (blocked) return false;

            // 到达终点
            if (x0 == x1 && y0 == y1) break;

            // Bresenham 步进
            int oldX = x0, oldY = y0;
            int e2 = 2 * err;
            bool changedX = false, changedY = false;

            if (e2 > -dy) { err -= dy; x0 += sx; changedX = true; }
            if (e2 < dx) { err += dx; y0 += sy; changedY = true; }

            // 斜角阻塞检测（防止穿墙角）
            if (changedX && changedY)
            {
                if (!IsWalkable(oldX + sx, oldY) || !IsWalkable(oldX, oldY + sy))
                    return false;
            }
        }
        return true;
    }
    // ========== A* 寻路（8方向，含对角线）==========
    private class Node
    {
        public Vector2Int pos;
        public Node parent;
        public int gCost;
        public int hCost;
        public int FCost => gCost + hCost;
    }

    // 8 方向：上/下/左/右 代价 10，对角线 代价 14（≈10√2）
    private static readonly Vector2Int[] Directions = {
        new Vector2Int(0, 1),   new Vector2Int(0, -1),
        new Vector2Int(-1, 0),  new Vector2Int(1, 0),
        new Vector2Int(-1, 1),  new Vector2Int(1, 1),
        new Vector2Int(-1, -1), new Vector2Int(1, -1),
    };
    private static readonly int[] DirectionCosts = { 10, 10, 10, 10, 14, 14, 14, 14 };

    // 对角线启发函数（Octile 距离）
    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * Mathf.Max(dx, dy) + 4 * Mathf.Min(dx, dy);
    }

    /// <summary> A* 寻路，返回世界坐标路径点列表 </summary>
    public List<Vector3> FindPath(Vector3 startWorld, Vector3 targetWorld)
    {
        Vector2Int start = WorldToGridIndex(startWorld);
        Vector2Int target = WorldToGridIndex(targetWorld);

        // 起点或终点不可通行时，找最近可通行点
        if (!IsWalkable(start)) start = FindNearestWalkable(start);
        if (!IsWalkable(target)) target = FindNearestWalkable(target);
        if (!IsWalkable(start) || !IsWalkable(target)) return null;
        if (start == target) return new List<Vector3> { GridIndexToWorld(start) };

        var openList = new List<Node>();
        var openDict = new Dictionary<Vector2Int, Node>(); // O(1) 查找
        var closedSet = new HashSet<Vector2Int>();

        Node startNode = new Node { pos = start, gCost = 0, hCost = Heuristic(start, target) };
        openList.Add(startNode);
        openDict[start] = startNode;

        while (openList.Count > 0)
        {
            // 取 F 值最小的节点
            int currentIndex = 0;
            Node current = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].FCost < current.FCost ||
                    (openList[i].FCost == current.FCost && openList[i].hCost < current.hCost))
                {
                    current = openList[i];
                    currentIndex = i;
                }
            }

            openList.RemoveAt(currentIndex);
            openDict.Remove(current.pos);
            closedSet.Add(current.pos);

            // 到达目标，回溯路径
            if (current.pos == target)
            {
                List<Vector3> path = new List<Vector3>();
                Node node = current;
                while (node != null)
                {
                    path.Add(GridIndexToWorld(node.pos));
                    node = node.parent;
                }
                path.Reverse();
                return path;
            }

            // 遍历 8 个方向
            for (int i = 0; i < 8; i++)
            {
                Vector2Int neighborPos = current.pos + Directions[i];

                if (closedSet.Contains(neighborPos) || !IsWalkable(neighborPos)) continue;

                // 对角线移动时，检查两侧格子是否可通行（防止穿墙角）
                if (i >= 4)
                {
                    Vector2Int side1 = new Vector2Int(current.pos.x + Directions[i].x, current.pos.y);
                    Vector2Int side2 = new Vector2Int(current.pos.x, current.pos.y + Directions[i].y);
                    if (!IsWalkable(side1) || !IsWalkable(side2)) continue;
                }

                int newGCost = current.gCost + DirectionCosts[i];

                if (openDict.TryGetValue(neighborPos, out Node existing))
                {
                    if (newGCost < existing.gCost)
                    {
                        existing.gCost = newGCost;
                        existing.parent = current;
                    }
                }
                else
                {
                    Node neighbor = new Node
                    {
                        pos = neighborPos,
                        parent = current,
                        gCost = newGCost,
                        hCost = Heuristic(neighborPos, target)
                    };
                    openList.Add(neighbor);
                    openDict[neighborPos] = neighbor;
                }
            }
        }

        return null; // 找不到路径
    }

    // ========== 最近可达点（A* 找不到路时的备选）==========
    public Vector2Int FindNearestWalkable(Vector2Int target)
    {
        if (IsWalkable(target)) return target;

        for (int radius = 1; radius < 20; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                    Vector2Int checkPos = target + new Vector2Int(dx, dy);
                    if (IsWalkable(checkPos)) return checkPos;
                }
            }
        }
        return target; // 保底
    }
}
