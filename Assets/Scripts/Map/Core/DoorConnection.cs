using UnityEngine;

/// <summary>
/// 门方向枚举
/// </summary>
public enum DoorDirection
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>
/// 门连接数据 — 描述一个门在房间内的位置
/// </summary>
[System.Serializable]
public class DoorConnection
{
    [Tooltip("门的方向")]
    public DoorDirection direction;

    [Tooltip("门在房间内的本地坐标")]
    public Vector2 localPosition;
}
