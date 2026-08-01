using UnityEngine;

/// <summary>
/// 房间门 — 挂载在每个门 GO 上。
/// 普通门：Trigger 检测玩家 → 切换房间。
/// 隐藏墙：清房后 openTrigger 启用但不放行玩家，子弹命中 N 次后墙壁破碎。
/// </summary>
public class RoomPortal : MonoBehaviour
{
    [Header("门配置")]
    public DoorDirection direction;
    public int targetRoomId = -1;

    [Header("组件引用")]
    [Tooltip("门 GO 的 SpriteRenderer")]
    public SpriteRenderer doorSprite;

    [Tooltip("开门/子弹检测的 Trigger Collider")]
    public Collider2D openTrigger;

    [Tooltip("关门/隐藏墙时激活的阻挡物")]
    public GameObject doorBlocker;

    [Header("触发设置")]
    public string targetTag = "Player";

    private bool _locked;
    private bool _isPermanentWall;
    private bool _breakable;
    private int _hiddenWallHP;
    private int _currentHP;

    private void Awake()
    {
        if (doorSprite == null) doorSprite = GetComponent<SpriteRenderer>();
        if (openTrigger == null) openTrigger = GetComponent<Collider2D>();
        if (openTrigger != null) openTrigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 可破坏墙壁：检测子弹
        if (_breakable && other.TryGetComponent<Projectile>(out var proj))
        {
            OnBulletHit(proj);
            return;
        }

        // 隐藏墙未破坏前不放行玩家 (_locked 为 true)
        if (_locked) return;

        // 正常的门传送
        if (targetRoomId < 0) return;
        if (!other.CompareTag(targetTag)) return;
        if (MapManager.Instance == null) return;
        if (other.GetComponent<PlayerStats>() == null) return;

        MapManager.Instance.SwitchRoom(targetRoomId, direction);
    }

    private void OnBulletHit(Projectile proj)
    {
        _currentHP--;
        Destroy(proj.gameObject);

        if (_currentHP <= 0)
            BreakWall();
    }

    private void BreakWall()
    {
        _breakable = false;
        _hiddenWallHP = 0;
        SetLocked(false);
        Debug.Log($"RoomPortal: 隐藏墙已破坏 (方向: {direction})");
    }

    /// <summary>设置为隐藏墙</summary>
    public void SetAsBreakableWall(int hp)
    {
        if (hp <= 0) return;
        _hiddenWallHP = hp;
        _currentHP = hp;
        _breakable = false;
        _locked = true;

        if (doorSprite != null) doorSprite.enabled = false;
        if (openTrigger != null) openTrigger.enabled = false;
        if (doorBlocker != null) doorBlocker.SetActive(true);
    }

    /// <summary>激活可破坏状态（清房后调用）：启用 openTrigger 接收子弹</summary>
    public void SetBreakable()
    {
        if (_hiddenWallHP <= 0 || _breakable) return;
        _breakable = true;
        if (openTrigger != null) openTrigger.enabled = true;
        Debug.Log($"RoomPortal: 隐藏墙可破坏 (方向: {direction}, HP: {_currentHP})");
    }

    public bool IsBreakable => _breakable;
    public int HiddenWallHP => _hiddenWallHP;

    /// <summary>锁定/解锁门</summary>
    public void SetLocked(bool locked)
    {
        if (_isPermanentWall) return;
        _locked = locked;

        if (openTrigger != null)
            openTrigger.enabled = !locked;

        if (doorSprite != null)
            doorSprite.enabled = !locked;

        if (doorBlocker != null)
            doorBlocker.SetActive(locked);
    }

    /// <summary>永久隐藏为墙壁（无连接房间）</summary>
    public void HideAsWall()
    {
        targetRoomId = -1;
        _isPermanentWall = true;
        _breakable = false;
        _hiddenWallHP = 0;

        if (openTrigger != null) openTrigger.enabled = false;
        if (doorSprite != null) doorSprite.enabled = false;
        if (doorBlocker != null) doorBlocker.SetActive(true);
    }
}
