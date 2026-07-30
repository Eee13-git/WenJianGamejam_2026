using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间根节点 — 挂载在每个房间预制体的根 GameObject 上。
/// 管理房间的激活/隐藏、门位置查询、生成点等。
/// </summary>
public class RoomRoot : MonoBehaviour
{
    [Header("房间配置")]
    public RoomConfig config;

    [Header("房间 ID (运行时赋值)")]
    public int roomId;

    [Header("门 Transform (按方向索引: Top=0, Bottom=1, Left=2, Right=3)")]
    public Transform topDoor;
    public Transform bottomDoor;
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("敌人生成点")]
    public Transform[] enemySpawnPoints;

    [Header("道具生成点")]
    public Transform[] itemSpawnPoints;

    [Header("房间边界 (用于摄像机)")]
    public Vector2 roomSize = new Vector2(20f, 12f);

    /// <summary>获取房间中心世界坐标</summary>
    public Vector2 Center => (Vector2)transform.position;

    /// <summary>获取房间边界矩形</summary>
    public Rect Bounds => new(Center - roomSize * 0.5f, roomSize);

    /// <summary>获取指定方向门的世界坐标</summary>
    public Vector3 GetDoorWorldPosition(DoorDirection dir)
    {
        var t = GetDoorTransform(dir);
        return t != null ? t.position : transform.position;
    }

    /// <summary>获取指定方向门的 Transform</summary>
    public Transform GetDoorTransform(DoorDirection dir)
    {
        return dir switch
        {
            DoorDirection.Top => topDoor,
            DoorDirection.Bottom => bottomDoor,
            DoorDirection.Left => leftDoor,
            DoorDirection.Right => rightDoor,
            _ => null
        };
    }

    /// <summary>获取所有可用的门方向</summary>
    public List<DoorDirection> GetAvailableDoors()
    {
        var doors = new List<DoorDirection>();
        if (topDoor != null) doors.Add(DoorDirection.Top);
        if (bottomDoor != null) doors.Add(DoorDirection.Bottom);
        if (leftDoor != null) doors.Add(DoorDirection.Left);
        if (rightDoor != null) doors.Add(DoorDirection.Right);
        return doors;
    }

    private List<GameObject> _childrenCache = new();

    /// <summary>激活房间 (显示所有子物体)</summary>
    public void Activate()
    {
        gameObject.SetActive(true);
    }

    /// <summary>隐藏房间</summary>
    public void Deactivate()
    {
        gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Center, roomSize);

        if (config != null)
        {
            var dirs = new[] { DoorDirection.Top, DoorDirection.Bottom, DoorDirection.Left, DoorDirection.Right };
            foreach (var dir in dirs)
            {
                if (config.HasDoor(dir))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(GetDoorWorldPosition(dir), 0.3f);
                }
            }
        }
    }
}
