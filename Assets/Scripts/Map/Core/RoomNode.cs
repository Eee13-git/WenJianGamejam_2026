using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间图节点 — 代表一个房间在图中的信息
/// </summary>
public class RoomNode
{
    /// <summary>房间 ID (唯一)</summary>
    public int roomId;

    /// <summary>房间配置</summary>
    public RoomConfig config;

    /// <summary>房间类型</summary>
    public RoomType roomType;

    /// <summary>房间在世界空间中的位置 (中心点)</summary>
    public Vector2 worldPosition;

    /// <summary>该房间已激活使用的门</summary>
    public List<DoorConnection> activeDoors = new();

    /// <summary>图连接: targetRoomId -> 通过哪个方向的门连接</summary>
    public Dictionary<int, DoorDirection> connections = new();

    /// <summary>房间的矩形边界 (世界坐标)</summary>
    public Rect Bounds => new(worldPosition - config.roomSize * 0.5f, config.roomSize);

    /// <summary>获取指定方向门的世界坐标</summary>
    public Vector2 GetDoorWorldPosition(DoorDirection dir)
    {
        return worldPosition + config.GetDoorOffset(dir);
    }
}
