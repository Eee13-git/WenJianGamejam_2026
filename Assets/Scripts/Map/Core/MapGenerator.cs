using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图生成器 — 基于配置生成房间图和布局
/// 
/// 算法:
/// 1. 从 Start 房间开始 (放置在中心)
/// 2. BFS 扩展: 每次从已放置房间的未占用门出发
/// 3. 随机选取匹配门方向的房间配置
/// 4. 根据门的本地坐标对齐相邻房间
/// 5. Boss 房间放在最远端, Exit 放在 Boss 旁边
/// </summary>
public class MapGenerator
{
    private MapConfig _config;
    private int _seed;

    public MapGenerator(MapConfig config)
    {
        _config = config;
    }

    /// <summary>生成房间图</summary>
    public RoomGraph Generate(int seed)
    {
        _seed = seed;
        if (_seed != 0)
            Random.InitState(_seed);
        else
            Random.InitState(System.DateTime.Now.Millisecond);

        var graph = new RoomGraph();

        if (_config.startRoom == null)
        {
            Debug.LogError("MapGenerator: startRoom 未配置!");
            return graph;
        }

        // 1. 放置 Start 房间在中心
        var startNode = graph.AddNode(_config.startRoom, RoomType.Start, Vector2.zero);
        graph.startRoomId = startNode.roomId;
        
        // 2. 构建需要生成的房间类型队列 (打乱顺序)
        var roomTypeQueue = BuildRoomTypeQueue();
        
        // 3. BFS 放置
        var placedOpenDoors = new List<(int roomId, DoorDirection dir)>();
        AddOpenDoors(placedOpenDoors, startNode);

        var placedRoomIds = new List<int> { startNode.roomId };
        int typeQueueIndex = 0;
        int failsafe = 0;
        const int maxAttempts = 200;

        while (typeQueueIndex < roomTypeQueue.Count && placedOpenDoors.Count > 0 && failsafe < maxAttempts)
        {
            failsafe++;

            // 随机选一个开放的门
            int doorIdx = Random.Range(0, placedOpenDoors.Count);
            var (anchorId, anchorDir) = placedOpenDoors[doorIdx];
            placedOpenDoors.RemoveAt(doorIdx);

            var anchorNode = graph.GetNode(anchorId);
            if (anchorNode == null) continue;

            RoomType targetType = roomTypeQueue[typeQueueIndex];
            RoomConfig[] pool = _config.GetPoolForType(targetType);

            if (pool == null || pool.Length == 0)
            {
                // 跳过没有配置该类型池的类型
                typeQueueIndex++;
                // 把这个门放回去，留给下一个房间
                placedOpenDoors.Add((anchorId, anchorDir));
                continue;
            }

            // 随机选取匹配门方向的房间
            var targetDir = RoomConfig.OppositeDir(anchorDir);
            var candidates = new List<RoomConfig>();
            foreach (var rc in pool)
            {
                if (rc != null && rc.HasDoor(targetDir))
                    candidates.Add(rc);
            }

            if (candidates.Count == 0)
            {
                // 没有可用的候选，跳过
                continue;
            }

            var chosenConfig = candidates[Random.Range(0, candidates.Count)];

            // 计算新房间位置
            Vector2 anchorDoorWorld = anchorNode.GetDoorWorldPosition(anchorDir);
            Vector2 targetDoorLocal = chosenConfig.GetDoorOffset(targetDir);
            Vector2 newPos = anchorDoorWorld - targetDoorLocal;

            // 碰撞检测
            var newBounds = new Rect(newPos - chosenConfig.roomSize * 0.5f, chosenConfig.roomSize);
            if (OverlapsAny(graph, newBounds))
            {
                // 放回门，稍后重试
                placedOpenDoors.Add((anchorId, anchorDir));
                continue;
            }

            // 成功放置
            var newNode = graph.AddNode(chosenConfig, targetType, newPos);
            graph.Connect(anchorId, newNode.roomId, anchorDir, targetDir);
            placedRoomIds.Add(newNode.roomId);
            
            // 将新房间的未连接门加入开放列表
            AddOpenDoors(placedOpenDoors, newNode, targetDir);
            
            // 如果是 Boss 房间，紧挨着放置 Exit
            if (targetType == RoomType.Boss && _config.exitRoom != null)
            {
                typeQueueIndex++;
                if (typeQueueIndex < roomTypeQueue.Count && roomTypeQueue[typeQueueIndex] == RoomType.Exit)
                {
                    if (TryPlaceExitRoom(graph, newNode, placedOpenDoors, placedRoomIds))
                        typeQueueIndex++;
                }
                continue;
            }

            typeQueueIndex++;
        }

        // 如果 Exit 还没放置，尝试最后放置
        if (graph.exitRoomId == 0 && _config.exitRoom != null)
        {
            PlaceExitRoomAtEnd(graph, placedOpenDoors);
        }

        if (failsafe >= maxAttempts)
            Debug.LogWarning("MapGenerator: 达到最大尝试次数，可能未生成全部房间");

        Debug.Log($"MapGenerator: 生成了 {graph.nodes.Count} 个房间 (预期 {_config.TotalRooms})");
        return graph;
    }

    /// <summary>构建房间类型队列 (打乱)</summary>
    private List<RoomType> BuildRoomTypeQueue()
    {
        var queue = new List<RoomType>();

        for (int i = 0; i < _config.normalRoomCount; i++)
            queue.Add(RoomType.Normal);
        for (int i = 0; i < _config.treasureRoomCount; i++)
            queue.Add(RoomType.Treasure);
        for (int i = 0; i < _config.shopRoomCount; i++)
            queue.Add(RoomType.Shop);
        // Boss 放最后，Exit 放 Boss 之后
        for (int i = 0; i < _config.bossRoomCount; i++)
            queue.Add(RoomType.Boss);
        // Exit 在 Boss 之后
        if (_config.exitRoom != null)
            queue.Add(RoomType.Exit);

        // 洗牌 (但不打乱 Boss 和 Exit 的相对顺序)
        // 把除了最后两个 (Boss + Exit) 的打乱
        int shuffleCount = queue.Count;
        if (_config.bossRoomCount > 0 && _config.exitRoom != null)
            shuffleCount -= 2;

        for (int i = 0; i < shuffleCount; i++)
        {
            int j = Random.Range(i, shuffleCount);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }

        return queue;
    }

    /// <summary>尝试在 Boss 房间旁放置 Exit</summary>
    private bool TryPlaceExitRoom(RoomGraph graph, RoomNode bossNode, 
        List<(int roomId, DoorDirection dir)> openDoors, List<int> placedRoomIds)
    {
        var dirs = new[] { DoorDirection.Top, DoorDirection.Bottom, DoorDirection.Left, DoorDirection.Right };
        Shuffle(dirs);

        foreach (var dir in dirs)
        {
            var oppositeDir = RoomConfig.OppositeDir(dir);

            if (!_config.exitRoom.HasDoor(oppositeDir))
                continue;

            // 检查 Boss 房间该方向是否已有连接
            if (bossNode.connections.ContainsValue(dir))
                continue;

            Vector2 anchorDoorWorld = bossNode.GetDoorWorldPosition(dir);
            Vector2 targetDoorLocal = _config.exitRoom.GetDoorOffset(oppositeDir);
            Vector2 newPos = anchorDoorWorld - targetDoorLocal;

            var newBounds = new Rect(newPos - _config.exitRoom.roomSize * 0.5f, _config.exitRoom.roomSize);
            if (OverlapsAny(graph, newBounds))
                continue;

            var exitNode = graph.AddNode(_config.exitRoom, RoomType.Exit, newPos);
            graph.Connect(bossNode.roomId, exitNode.roomId, dir, oppositeDir);
            graph.exitRoomId = exitNode.roomId;
            return true;
        }

        return false;
    }

    /// <summary>最后放置 Exit 房间</summary>
    private void PlaceExitRoomAtEnd(RoomGraph graph, 
        List<(int roomId, DoorDirection dir)> openDoors)
    {
        if (graph.exitRoomId != 0) return;

        // 找 Boss 房间
        RoomNode bossNode = null;
        foreach (var n in graph.nodes)
        {
            if (n.roomType == RoomType.Boss)
            {
                bossNode = n;
                break;
            }
        }

        if (bossNode != null)
        {
            if (TryPlaceExitRoom(graph, bossNode, openDoors, null))
                return;
        }

        // 回退: 从任意开放的门放置
        foreach (var openDoor in openDoors)
        {
            var anchorNode = graph.GetNode(openDoor.roomId);
            if (anchorNode == null) continue;

            var dirs = new[] { DoorDirection.Top, DoorDirection.Bottom, DoorDirection.Left, DoorDirection.Right };
            Shuffle(dirs);

            foreach (var dir in dirs)
            {
                var opp = RoomConfig.OppositeDir(dir);
                if (!_config.exitRoom.HasDoor(opp)) continue;

                Vector2 anchorDoorWorld = anchorNode.GetDoorWorldPosition(dir);
                Vector2 targetDoorLocal = _config.exitRoom.GetDoorOffset(opp);
                Vector2 newPos = anchorDoorWorld - targetDoorLocal;
                var newBounds = new Rect(newPos - _config.exitRoom.roomSize * 0.5f, _config.exitRoom.roomSize);
                if (OverlapsAny(graph, newBounds)) continue;

                var exitNode = graph.AddNode(_config.exitRoom, RoomType.Exit, newPos);
                graph.Connect(anchorNode.roomId, exitNode.roomId, dir, opp);
                graph.exitRoomId = exitNode.roomId;
                return;
            }
        }
    }

    /// <summary>将房间的未使用门加入开放列表</summary>
    private void AddOpenDoors(List<(int roomId, DoorDirection dir)> openDoors, RoomNode node, 
        DoorDirection? excludeDir = null)
    {
        var allDirs = new[] { DoorDirection.Top, DoorDirection.Bottom, DoorDirection.Left, DoorDirection.Right };
        foreach (var dir in allDirs)
        {
            if (excludeDir.HasValue && dir == excludeDir.Value) continue;
            if (node.config.HasDoor(dir))
                openDoors.Add((node.roomId, dir));
        }
    }

    /// <summary>检查矩形是否与已有房间重叠。
    /// 相邻房间共享边界 (edge-to-edge) 不算重叠，只有真正交叉才冲突。
    /// roomSpacing 用作最小间隔。</summary>
    private bool OverlapsAny(RoomGraph graph, Rect bounds)
    {
        // 缩一圈来确保最小间距 (只对真正相交的生效)
        float shrink = _config.roomSpacing * 0.5f;
        var checkRect = new Rect(
            bounds.x + shrink,
            bounds.y + shrink,
            bounds.width - shrink * 2,
            bounds.height - shrink * 2
        );

        foreach (var node in graph.nodes)
        {
            var otherRect = new Rect(
                node.Bounds.x + shrink,
                node.Bounds.y + shrink,
                node.Bounds.width - shrink * 2,
                node.Bounds.height - shrink * 2
            );

            if (checkRect.Overlaps(otherRect))
                return true;
        }
        return false;
    }

    private static void Shuffle<T>(T[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
