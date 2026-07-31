using UnityEngine;

/// <summary>
/// 房间门 — 挂载在每个门 GO 上。
/// 门 GO 本身是 Trigger + 门贴图；Blocker 是 Solid Collider + 墙壁贴图。
/// 无连接时：隐藏门 GO、显示 Blocker，视觉上变回墙壁。
/// </summary>
public class RoomPortal : MonoBehaviour
{
    [Header("门配置")]
    public DoorDirection direction;
    public int targetRoomId = -1;

    [Header("组件引用")]
    [Tooltip("门 GO 的 SpriteRenderer (半透明门贴图)")]
    public SpriteRenderer doorSprite;

    [Tooltip("开门时激活的 Trigger Collider")]
    public Collider2D openTrigger;

    [Tooltip("关门/无连接时激活的阻挡物 — 实体碰撞体")]
    public GameObject doorBlocker;

    [Header("触发设置")]
    public string targetTag = "Player";

    private bool _locked;

    private void Awake()
    {
        if (doorSprite == null) doorSprite = GetComponent<SpriteRenderer>();
        if (openTrigger == null) openTrigger = GetComponent<Collider2D>();
        if (openTrigger != null) openTrigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_locked) return;
        if (targetRoomId < 0) return;
        if (!other.CompareTag(targetTag)) return;
        if (MapManager.Instance == null) return;

        // 只有真正的主角才能触发传送（随从也是 Player tag，但不应该切换房间）
        if (other.GetComponent<PlayerStats>() == null) return;

        MapManager.Instance.SwitchRoom(targetRoomId, direction);
    }

    /// <summary>锁定/解锁门。locked=true: 启用 Blocker、禁用 Trigger + 隐藏门贴图。</summary>
    public void SetLocked(bool locked)
    {
        _locked = locked;

        if (openTrigger != null)
            openTrigger.enabled = !locked;

        if (doorSprite != null)
            doorSprite.enabled = !locked;

        if (doorBlocker != null)
            doorBlocker.SetActive(locked);
    }

    /// <summary>将此门永久隐藏为墙壁（无连接房间）。</summary>
    public void HideAsWall()
    {
        targetRoomId = -1;

        // 隐藏门的 Trigger + Sprite
        if (openTrigger != null) openTrigger.enabled = false;
        if (doorSprite != null) doorSprite.enabled = false;

        // 启用 Blocker 填充门洞
        if (doorBlocker != null) doorBlocker.SetActive(true);
    }
}
