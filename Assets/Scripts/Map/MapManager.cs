using System.Collections;
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

    private BoundsInt bounds; // 地图边界

    private void Awake()
    {
        // 单例初始化
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // 自动获取组件（如果没手动拖拽）
        if (grid == null) grid = GetComponent<Grid>();
        if (obstacleTilemap != null)
        {
            bounds = obstacleTilemap.cellBounds; // 获取障碍物地图的边界
        }
    }

    // ---------- 核心转换方法 ----------

    /// <summary> 世界坐标 -> 格子坐标 </summary>
    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return grid.WorldToCell(worldPosition);
    }

    /// <summary> 格子坐标 -> 世界坐标（格子中心点） </summary>
    public Vector3 CellToWorld(Vector3Int cellPosition)
    {
        return grid.GetCellCenterWorld(cellPosition);
    }

    // ---------- 逻辑判定方法 ----------

    /// <summary> 判断某个世界坐标是否可通行（没有障碍物） </summary>
    public bool IsWalkable(Vector3 worldPosition)
    {
        Vector3Int cellPos = WorldToCell(worldPosition);

        // 1. 检查是否有障碍物 Tile
        if (obstacleTilemap != null)
        {
            TileBase tile = obstacleTilemap.GetTile(cellPos);
            if (tile != null)
                return false; // 有砖块 = 不可通行
        }

        // 2. (可选) 检查是否超出地图边界，防止角色跑到地图外面
        // 注意：如果地图很大，你可以取消注释下面的边界检查
        // if (!bounds.Contains(cellPos)) return false;

        return true;
    }

    /// <summary> 判断某个格子坐标是否被障碍物占据 </summary>
    public bool IsCellBlocked(Vector3Int cellPos)
    {
        if (obstacleTilemap == null) return false;
        return obstacleTilemap.GetTile(cellPos) != null;
    }

    // ---------- 工具方法 ----------

    /// <summary> 获取某个格子上的 Tile 引用（可用来做陷阱、道具识别） </summary>
    public TileBase GetTileAtWorld(Vector3 worldPosition)
    {
        Vector3Int cellPos = WorldToCell(worldPosition);
        if (groundTilemap != null)
            return groundTilemap.GetTile(cellPos);
        return null;
    }
}