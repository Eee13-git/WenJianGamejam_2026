using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间图 — 描述所有房间及其连接关系
/// </summary>
[System.Serializable]
public class RoomGraph
{
    /// <summary>所有房间节点</summary>
    public List<RoomNode> nodes = new();

    /// <summary>起始房间 ID</summary>
    public int startRoomId;

    private int _nextId;

    /// <summary>添加房间节点</summary>
    public RoomNode AddNode(RoomConfig config, RoomType type, Vector2 worldPos)
    {
        var node = new RoomNode
        {
            roomId = _nextId++,
            config = config,
            roomType = type,
            worldPosition = worldPos
        };
        nodes.Add(node);
        return node;
    }

    /// <summary>连接两个房间的门</summary>
    public void Connect(int fromId, int toId, DoorDirection fromDir, DoorDirection toDir)
    {
        var from = GetNode(fromId);
        var to = GetNode(toId);
        if (from == null || to == null) return;

        from.connections[toId] = fromDir;
        to.connections[fromId] = toDir;
    }

    /// <summary>根据 ID 获取节点</summary>
    public RoomNode GetNode(int id)
    {
        return nodes.Find(n => n.roomId == id);
    }

    /// <summary>获取相邻房间列表</summary>
    public List<RoomNode> GetAdjacent(int id)
    {
        var node = GetNode(id);
        if (node == null) return new List<RoomNode>();

        var result = new List<RoomNode>();
        foreach (var conn in node.connections)
        {
            var adj = GetNode(conn.Key);
            if (adj != null) result.Add(adj);
        }
        return result;
    }
}
