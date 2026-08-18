using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图管理器 (单例) — 新地图系统的核心管理器。
/// 替代旧的 TileManager。
/// 负责: 地图生成、房间实例化、房间切换、玩家追踪。
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Header("地图配置")]
    [SerializeField] private MapConfig _mapConfig;

    [Header("运行时")]
    [SerializeField] private int _currentRoomId;
    [SerializeField] private bool _isSwitchingRoom;

    // 内部状态
    private RoomGraph _roomGraph;
    private MapGenerator _generator;
    private Dictionary<int, RoomRoot> _roomInstances = new();
    private RoomRoot _currentRoom;
    private Coroutine _switchCoroutine;
    private Transform _playerTransform;

    /// <summary>玩家从门进入新房间时的内缩距离 (避免立刻再次触发门)</summary>
    [SerializeField] private float _doorEntryOffset = 0f;

    // 事件
    public event System.Action<int, int> OnRoomChanged; // (fromRoomId, toRoomId)
    public event System.Action<int, int> OnRoomSwitchStarted;
    public event System.Action<int> OnRoomSwitchCompleted; // (newRoomId)

    // 属性
    public RoomRoot CurrentRoom => _currentRoom;
    public int CurrentRoomId => _currentRoomId;
    public bool IsSwitchingRoom => _isSwitchingRoom;
    public RoomGraph RoomGraph => _roomGraph;
    public MapConfig MapConfigAsset => _mapConfig;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 缓存玩家 Transform
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
            _playerTransform = playerGo.transform;

        if (_mapConfig != null)
        {
            GenerateMap();
        }
        else
        {
            Debug.LogError("MapManager: MapConfig 未配置!");
        }
    }

    // ==================== 地图生成 ====================

    /// <summary>生成并实例化地图</summary>
    public void GenerateMap()
    {
        if (_mapConfig == null)
        {
            Debug.LogError("MapManager: MapConfig 为空，无法生成地图");
            return;
        }

        // 清理旧实例
        ClearExistingRooms();

        // 设置随机种子
        int seed = _mapConfig.useRandomSeed ? Random.Range(0, int.MaxValue) : _mapConfig.seed;

        // 生成房间图
        _generator = new MapGenerator(_mapConfig);
        _roomGraph = _generator.Generate(seed);

        // 实例化所有房间
        foreach (var node in _roomGraph.nodes)
        {
            InstantiateRoom(node);
        }

        // 设置所有 RoomPortal 的目标房间
        SetupPortals();

        // 先隐藏所有房间
        foreach (var room in _roomInstances.Values)
        {
            room.Deactivate();
        }

        // 激活起始房间
        var startNode = _roomGraph.GetNode(_roomGraph.startRoomId);
        if (startNode != null && _roomInstances.TryGetValue(_roomGraph.startRoomId, out var startRoom))
        {
            startRoom.Activate();
            _currentRoomId = startRoom.roomId;
            _currentRoom = startRoom;

            // 通知 RoomManager 玩家进入
            var roomMgr = startRoom.GetComponent<RoomManager>();
            if (roomMgr != null)
                roomMgr.OnPlayerEnter();
        }

        Debug.Log($"MapManager: 地图生成完成 — {_roomGraph.nodes.Count} 个房间, Start={_roomGraph.startRoomId}");
    }

    /// <summary>实例化单个房间</summary>
    private void InstantiateRoom(RoomNode node)
    {
        var prefab = node.config.GetRandomPrefab();
        if (prefab == null)
        {
            Debug.LogError($"MapManager: RoomConfig '{node.config.name}' 没有预制体!");
            return;
        }

        var go = Instantiate(prefab, node.worldPosition, Quaternion.identity, transform);
        go.name = $"Room_{node.roomId}_{node.roomType}";

        var root = go.GetComponent<RoomRoot>();
        if (root == null)
        {
            root = go.AddComponent<RoomRoot>();
        }

        root.config = node.config;
        root.roomId = node.roomId;

        // 确保有 RoomManager
        var roomMgr = go.GetComponent<RoomManager>();
        if (roomMgr == null)
            go.AddComponent<RoomManager>();

        _roomInstances[node.roomId] = root;
    }

    /// <summary>设置所有门的连接目标；无连接的门伪装为墙壁。</summary>
    private void SetupPortals()
    {
        foreach (var node in _roomGraph.nodes)
        {
            if (!_roomInstances.TryGetValue(node.roomId, out var root))
                continue;

            var portals = root.GetComponentsInChildren<RoomPortal>(true);
            foreach (var portal in portals)
            {
                // 查找匹配的连接
                bool connected = false;
                foreach (var conn in node.connections)
                {
                    if (conn.Value == portal.direction)
                    {
                        portal.targetRoomId = conn.Key;

                        // 如果目标房间是隐藏房，设置为可破坏墙壁
                        var targetNode = _roomGraph.GetNode(conn.Key);
                        if (targetNode != null && targetNode.roomType == RoomType.Hidden)
                        {
                            int hp = targetNode.config.hiddenWallHP > 0 ? targetNode.config.hiddenWallHP : 4;
                            portal.SetAsBreakableWall(hp);
                        }

                        connected = true;
                        break;
                    }
                }

                if (!connected)
                {
                    portal.HideAsWall();
                }
            }
        }
    }

    // ==================== 房间切换 ====================

    /// <summary>切换到目标房间，玩家从指定方向的门进入</summary>
    public void SwitchRoom(int targetRoomId, DoorDirection entryDir)
    {
        if (_isSwitchingRoom) return;
        if (targetRoomId == _currentRoomId) return;
        if (!_roomInstances.TryGetValue(targetRoomId, out var targetRoom)) return;

        StartCoroutine(SwitchRoomRoutine(targetRoomId, targetRoom, entryDir));
    }

    private IEnumerator SwitchRoomRoutine(int targetRoomId, RoomRoot targetRoom, DoorDirection entryDir)
    {
        _isSwitchingRoom = true;
        int fromRoomId = _currentRoomId;

        OnRoomSwitchStarted?.Invoke(fromRoomId, targetRoomId);

        // 通知旧房间玩家离开
        if (_currentRoom != null)
        {
            var oldMgr = _currentRoom.GetComponent<RoomManager>();
            oldMgr?.OnPlayerExit();
        }

        // 激活新房间
        targetRoom.Activate();

        // 传送玩家到目标房间入口 (反方向门的内侧)
        DoorDirection targetEntryDir = RoomConfig.OppositeDir(entryDir);
        TeleportPlayerToDoor(targetRoom, targetEntryDir);

        // 通知新房间玩家进入
        var newMgr = targetRoom.GetComponent<RoomManager>();
        newMgr?.OnPlayerEnter();

        // 更新状态
        _currentRoom = targetRoom;
        _currentRoomId = targetRoomId;

        // 等待摄像机过渡完成
        var cameraCtrl = Camera.main != null ? Camera.main.GetComponent<RoomCameraController>() : null;
        if (cameraCtrl != null)
        {
            bool transitionDone = false;
            cameraCtrl.MoveToRoom(targetRoom, () => transitionDone = true);
            yield return new WaitUntil(() => transitionDone);
        }

        // 隐藏旧房间
        foreach (var kvp in _roomInstances)
        {
            if (kvp.Key != _currentRoomId)
                kvp.Value.Deactivate();
        }

        _isSwitchingRoom = false;
        OnRoomChanged?.Invoke(fromRoomId, targetRoomId);
        OnRoomSwitchCompleted?.Invoke(targetRoomId);

        // 统计：访问房间已由 RoomManager.OnPlayerEnter（_isFirstEnter 守卫）负责，此处不再重复

        Debug.Log($"MapManager: 切换到房间 {targetRoomId} ({targetRoom.config.roomType})");
    }

    /// <summary>将玩家传送到目标房间入口门内侧的地板中央位置</summary>
    private void TeleportPlayerToDoor(RoomRoot room, DoorDirection entryDir)
    {
        if (_playerTransform == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) _playerTransform = playerGo.transform;
        }
        if (_playerTransform == null) return;

        // 玩家从 entryDir 方向的门进入，放置在距离墙壁 _doorEntryOffset 格的位置
        Vector2 halfSize = room.roomSize * 0.5f;
        float offset = halfSize.y - _doorEntryOffset;
        if (entryDir is DoorDirection.Left or DoorDirection.Right)
            offset = halfSize.x - _doorEntryOffset;

        // 玩家从 entryDir 方向的门进入，放置在房间中心朝入口门方向偏移
        Vector2 doorDir = entryDir switch
        {
            DoorDirection.Top => Vector2.up,
            DoorDirection.Bottom => Vector2.down,
            DoorDirection.Left => Vector2.left,
            DoorDirection.Right => Vector2.right,
            _ => Vector2.zero
        };

        Vector2 targetPos = room.Center + doorDir * offset;

        TeleportRigidbody(_playerTransform, targetPos);
        TeleportFollowers(targetPos, doorDir, room.transform);
    }

    /// <summary>安全传送 Transform（优先通过 Rigidbody2D.position）</summary>
    private static void TeleportRigidbody(Transform t, Vector2 targetPos)
    {
        if (t == null) return;
        var rb = t.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.position = targetPos;
        }
        else
        {
            t.position = targetPos;
        }
    }

    /// <summary>将所有活跃随从传送到玩家两侧和侧前方（正前方留空不挡路），阵型跟随面朝方向旋转</summary>
    private static void TeleportFollowers(Vector2 playerPos, Vector2 playerForward, Transform newParent)
    {
        var followers = EnemyFollower.ActiveFollowers;
        int count = followers.Count;
        if (count == 0) return;

        float angle = Mathf.Atan2(playerForward.y, playerForward.x) - Mathf.PI / 2f;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        float halfPi = Mathf.PI / 2f;
        float gapAngle = 0.25f; // 正前方留空约 14°，不挡玩家视线和移动

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : -0.5f;

            // 在 -90°~90° 之间分布，但跳过正前方 ±gap 区域
            float rad;
            if (count == 1)
            {
                rad = -halfPi + gapAngle; // 单个随从放左侧
            }
            else if (t <= 0.5f)
            {
                rad = Mathf.Lerp(-halfPi, -gapAngle, t * 2f);   // 左侧到左前方
            }
            else
            {
                rad = Mathf.Lerp(gapAngle, halfPi, (t - 0.5f) * 2f); // 右前方到右侧
            }

            float sideX = Mathf.Sin(rad);
            float forwardY = Mathf.Cos(rad);
            Vector2 localOffset = new Vector2(sideX, forwardY) * 1.2f;

            Vector2 rotatedOffset = new Vector2(
                localOffset.x * cos - localOffset.y * sin,
                localOffset.x * sin + localOffset.y * cos
            );

            Vector2 targetPos = playerPos + rotatedOffset;
            TeleportRigidbody(followers[i].transform, targetPos);
        }
    }

    // ==================== 查询 API ====================

    /// <summary>根据 ID 获取房间</summary>
    public RoomRoot GetRoom(int roomId)
    {
        _roomInstances.TryGetValue(roomId, out var room);
        return room;
    }

    /// <summary>获取相邻房间列表</summary>
    public List<RoomRoot> GetAdjacentRooms(int roomId)
    {
        var result = new List<RoomRoot>();
        var node = _roomGraph?.GetNode(roomId);
        if (node == null) return result;

        foreach (var conn in node.connections)
        {
            if (_roomInstances.TryGetValue(conn.Key, out var room))
                result.Add(room);
        }
        return result;
    }

    /// <summary>确保所有门的连接正确 (在编辑器修改后调用)</summary>
    public void RefreshPortals()
    {
        SetupPortals();
    }

    // ==================== 辅助 ====================

    private void ClearExistingRooms()
    {
        // 1. 清理已追踪的房间
        foreach (var kvp in _roomInstances)
        {
            if (kvp.Value != null)
                DestroyImmediate(kvp.Value.gameObject);
        }
        _roomInstances.Clear();

        // 2. 清理残余子物体 (场景中保存的旧房间)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (child.GetComponent<RoomRoot>() != null)
                DestroyImmediate(child);
        }

        _roomGraph = null;
        _currentRoom = null;
    }

    /// <summary>在编辑器中重新生成 (通过 Context Menu 调用)</summary>
    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        ClearExistingRooms();
        GenerateMap();
    }
}
