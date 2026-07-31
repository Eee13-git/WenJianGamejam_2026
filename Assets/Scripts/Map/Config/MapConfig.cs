using UnityEngine;

/// <summary>
/// 地图生成配置 — 全局地牢生成参数
/// </summary>
[CreateAssetMenu(fileName = "MapConfig", menuName = "Map/Map Config")]
public class MapConfig : ScriptableObject
{
    [Header("随机种子")]
    [Tooltip("是否使用随机种子")]
    public bool useRandomSeed = true;

    [Tooltip("固定种子 (useRandomSeed=false 时生效)")]
    public int seed;

    [Header("房间数量")]
    [Tooltip("普通战斗房间数量")]
    public int normalRoomCount = 6;

    [Tooltip("宝藏房间数量")]
    public int treasureRoomCount = 1;

    [Tooltip("商店房间数量")]
    public int shopRoomCount = 1;

    [Header("最终房间")]
    [Tooltip("是否有 Boss 房间。true=Boss, false=Exit (二选一)")]
    public bool hasBoss = true;

    [Tooltip("Boss 房间数量 (hasBoss=true 时生效)")]
    public int bossRoomCount = 1;

    /// <summary>总房间数 (Start + 普通/宝藏/商店 + 最终Boss或Exit + 隐藏房)</summary>
    public int TotalRooms => normalRoomCount + treasureRoomCount + shopRoomCount + (hasBoss ? bossRoomCount : 1) + hiddenRoomCount + 1; // +1 for Start

    [Header("各类型可选房间预制体池")]
    [Tooltip("普通房间池")]
    public RoomConfig[] normalRoomPool;

    [Tooltip("宝藏房间池")]
    public RoomConfig[] treasureRoomPool;

    [Tooltip("Boss 房间池")]
    public RoomConfig[] bossRoomPool;

    [Tooltip("商店房间池")]
    public RoomConfig[] shopRoomPool;

    [Tooltip("隐藏房间池")]
    public RoomConfig[] hiddenRoomPool;

    [Tooltip("隐藏房数量")]
    public int hiddenRoomCount = 1;

    [Tooltip("起始房间 (固定)")]
    public RoomConfig startRoom;

    [Tooltip("出口房间 (固定)")]
    public RoomConfig exitRoom;

    [Header("布局参数")]
    [Tooltip("房间间距 (两房间之间的额外间距)")]
    public float roomSpacing = 0.5f;

    /// <summary>根据房间类型获取可选房间池</summary>
    public RoomConfig[] GetPoolForType(RoomType type)
    {
        return type switch
        {
            RoomType.Normal => normalRoomPool,
            RoomType.Treasure => treasureRoomPool,
            RoomType.Boss => bossRoomPool,
            RoomType.Shop => shopRoomPool,
            RoomType.Hidden => hiddenRoomPool,
            _ => null
        };
    }
}
