using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间配置 — 定义某一类房间的通用参数 + 多个可选预制体变体
/// </summary>
[CreateAssetMenu(fileName = "RoomConfig", menuName = "Map/Room Config")]
public class RoomConfig : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("房间类型")]
    public RoomType roomType;

    [Tooltip("可选房间预制体列表 (生成时随机选取一个)")]
    public GameObject[] roomPrefabs;

    [Tooltip("难度等级 (影响怪物属性缩放等)")]
    public int difficultyLevel = 1;

    [Header("连接点 — 该房间有哪些方向的门")]
    public bool hasTopDoor;
    public bool hasBottomDoor;
    public bool hasLeftDoor;
    public bool hasRightDoor;

    [Header("房间尺寸 (世界单位，用于摄像机边界和布局计算)")]
    public Vector2 roomSize = new Vector2(20f, 12f);

    [Header("门在房间内的本地坐标偏移")]
    public Vector2 topDoorOffset = new Vector2(0f, 6f);
    public Vector2 bottomDoorOffset = new Vector2(0f, -6f);
    public Vector2 leftDoorOffset = new Vector2(-10f, 0f);
    public Vector2 rightDoorOffset = new Vector2(10f, 0f);

    [Header("怪物生成")]
    [Tooltip("可选怪物池")]
    public List<EnemySpawnEntry> enemyPool = new();

    [Tooltip("最少生成怪物数")]
    public int minEnemies = 1;

    [Tooltip("最多生成怪物数")]
    public int maxEnemies = 3;

    [Header("道具生成")]
    [Tooltip("可选道具池")]
    public List<GameObject> itemPool = new();

    [Tooltip("最少道具数")]
    public int minItems = 0;

    [Tooltip("最多道具数")]
    public int maxItems = 2;

    /// <summary>获取指定方向门的本地坐标偏移</summary>
    public Vector2 GetDoorOffset(DoorDirection dir)
    {
        return dir switch
        {
            DoorDirection.Top => topDoorOffset,
            DoorDirection.Bottom => bottomDoorOffset,
            DoorDirection.Left => leftDoorOffset,
            DoorDirection.Right => rightDoorOffset,
            _ => Vector2.zero
        };
    }

    /// <summary>检查是否有指定方向的门</summary>
    public bool HasDoor(DoorDirection dir)
    {
        return dir switch
        {
            DoorDirection.Top => hasTopDoor,
            DoorDirection.Bottom => hasBottomDoor,
            DoorDirection.Left => hasLeftDoor,
            DoorDirection.Right => hasRightDoor,
            _ => false
        };
    }

    /// <summary>获取门的相反方向</summary>
    public static DoorDirection OppositeDir(DoorDirection dir)
    {
        return dir switch
        {
            DoorDirection.Top => DoorDirection.Bottom,
            DoorDirection.Bottom => DoorDirection.Top,
            DoorDirection.Left => DoorDirection.Right,
            DoorDirection.Right => DoorDirection.Left,
            _ => DoorDirection.Top
        };
    }

    /// <summary>从预制体池中随机选取一个。池为空时返回 null。</summary>
    public GameObject GetRandomPrefab()
    {
        if (roomPrefabs == null || roomPrefabs.Length == 0) return null;
        return roomPrefabs[Random.Range(0, roomPrefabs.Length)];
    }
}
