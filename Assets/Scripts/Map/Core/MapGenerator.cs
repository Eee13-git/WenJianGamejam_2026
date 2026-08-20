using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图生成器 — 基于配置生成房间图谱和布局
/// 
/// 算法:
/// 1. 从 Start 房间开始 (放置在中心)
/// 2. BFS 扩展: 每次从已放置房间的未占用门出口
/// 3. 随机选取匹配门方向的房间配置
/// 4. 根据门的本地坐标对齐相邻房间
/// 5. Boss 放最后，隐藏房从非终点的开放门放置
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
            Debug.LogError("MapGenerator: startRoom 未配置");
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

            // Boss 不与 Start 房间直连，跳过该门
            if (targetType == RoomType.Boss && anchorNode.roomType == RoomType.Start)
                continue;

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

            typeQueueIndex++;
        }

        // 放置隐藏房：从非 Boss 房间的开放门中随机选取放置
        PlaceHiddenRooms(graph, placedOpenDoors);

        if (failsafe >= maxAttempts)
            Debug.LogWarning("MapGenerator: 达到最大尝试次数，可能未生成全部房间");

        Debug.Log($"MapGenerator: 生成了 {graph.nodes.Count} 个房间 (预期 {_config.TotalRooms})");
        return graph;
    }

    /// <summary>构建房间类型队列 (打乱，最终 Boss 放最后)</summary>
    private List<RoomType> BuildRoomTypeQueue()
    {
        var queue = new List<RoomType>();

        for (int i = 0; i < _config.normalRoomCount; i++)
            queue.Add(RoomType.Normal);
        for (int i = 0; i < _config.treasureRoomCount; i++)
            queue.Add(RoomType.Treasure);
        for (int i = 0; i < _config.shopRoomCount; i++)
            queue.Add(RoomType.Shop);

        // 最终房间：Boss 放最后
        for (int i = 0; i < _config.bossRoomCount; i++)
            queue.Add(RoomType.Boss);

        // 打乱除最后一个 (Boss) 以外的所有房间
        int shuffleCount = queue.Count - 1;
        for (int i = 0; i < shuffleCount; i++)
        {
            int j = Random.Range(i, shuffleCount);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }

        return queue;
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

    /// <summary>放置隐藏房：从非 Boss 房间的开放门中随机选取放置</summary>
    private void PlaceHiddenRooms(RoomGraph graph, 
        List<(int roomId, DoorDirection dir)> openDoors)
    {
        if (_config.hiddenRoomPool == null || _config.hiddenRoomPool.Length == 0) return;
        int count = _config.hiddenRoomCount;
        if (count <= 0) return;

        // 收集非 Boss 房间的开放门
        var eligibleDoors = new List<(int roomId, DoorDirection dir)>();
        foreach (var od in openDoors)
        {
            var node = graph.GetNode(od.roomId);
            if (node == null) continue;
            if (node.roomType == RoomType.Boss) continue;
            eligibleDoors.Add(od);
        }

        if (eligibleDoors.Count == 0) return;

        int placed = 0;
        int failsafe = 0;
        const int maxAttempts = 50;

        while (placed < count && eligibleDoors.Count > 0 && failsafe < maxAttempts)
        {
            failsafe++;

            // 随机选一个可用的开放门
            int idx = Random.Range(0, eligibleDoors.Count);
            var (anchorId, anchorDir) = eligibleDoors[idx];
            eligibleDoors.RemoveAt(idx);

            var anchorNode = graph.GetNode(anchorId);
            if (anchorNode == null) continue;

            var targetDir = RoomConfig.OppositeDir(anchorDir);
            
            // 选取匹配的隐藏房配置
            var candidates = new List<RoomConfig>();
            foreach (var rc in _config.hiddenRoomPool)
            {
                if (rc != null && rc.HasDoor(targetDir))
                    candidates.Add(rc);
            }
            if (candidates.Count == 0) continue;

            var chosenConfig = candidates[Random.Range(0, candidates.Count)];

            Vector2 anchorDoorWorld = anchorNode.GetDoorWorldPosition(anchorDir);
            Vector2 targetDoorLocal = chosenConfig.GetDoorOffset(targetDir);
            Vector2 newPos = anchorDoorWorld - targetDoorLocal;

            var newBounds = new Rect(newPos - chosenConfig.roomSize * 0.5f, chosenConfig.roomSize);
            if (OverlapsAny(graph, newBounds)) continue;

            var newNode = graph.AddNode(chosenConfig, RoomType.Hidden, newPos);
            graph.Connect(anchorId, newNode.roomId, anchorDir, targetDir);
            // 不把隐藏房的门加入开放列表 (保证只有一个连接)
            placed++;
        }

        if (placed < count)
            Debug.LogWarning($"MapGenerator: 只放置了 {placed}/{count} 个隐藏房");
    }

    /// <summary>检查矩形是否与已有房间重叠。
    /// 相邻房间共享边界 (edge-to-edge) 不算重叠，只有真正交叉才算冲突。
    /// roomSpacing 用作最小间距。
    /// </summary>
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
}
