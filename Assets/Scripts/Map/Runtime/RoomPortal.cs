using UnityEngine;

/// <summary>
/// 房间门 — 挂载在每个门 GO 上。
/// 普通门：Update 中基于距离+输入方向检测玩家是否朝门移动 → 切换房间。
/// 隐藏墙：清房后 openTrigger 启用接收子弹，命中 N 次后墙壁破碎，显示门。
/// 无连接的门：直接隐藏，不出现墙壁贴图。
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
    private bool _hidden;       // 无连接，永久隐藏
    private bool _breakable;
    private int _hiddenWallHP;
    private int _currentHP;

    // 惰性缓存的玩家引用 (零 Find, 零 GC)
    private static PlayerController _cachedPlayer;

    private void Awake()
    {
        if (doorSprite == null) doorSprite = GetComponent<SpriteRenderer>();
        if (openTrigger == null) openTrigger = GetComponent<Collider2D>();
        if (openTrigger != null) openTrigger.isTrigger = true;
    }

    private void Update()
    {
        if (_hidden || _locked || targetRoomId < 0) return;
        if (_breakable) return;
        if (MapManager.Instance == null) return;
        if (MapManager.Instance.IsSwitchingRoom) return;
        if (!MapManager.Instance.CanTriggerPortal()) return;

        // 惰性缓存 PlayerController
        if (_cachedPlayer == null || _cachedPlayer.gameObject == null)
        {
            var player = PlayerManager.Instance?.CurrentPlayer;
            if (player == null) return;
            _cachedPlayer = player.GetComponent<PlayerController>();
            if (_cachedPlayer == null) return;
        }

        Vector2 playerPos = _cachedPlayer.transform.position;
        Vector2 doorPos = transform.position;
        Vector2 inputDir = _cachedPlayer.MoveDirection;

        float dx = Mathf.Abs(playerPos.x - doorPos.x);
        float dy = Mathf.Abs(playerPos.y - doorPos.y);

        float thresholdY = MapManager.Instance.PortalEnterThresholdY;
        float thresholdX = MapManager.Instance.PortalEnterThresholdX;
        float minInput = MapManager.Instance.PortalMinInput;

        bool inRange, movingTowardDoor;
        switch (direction)
        {
            case DoorDirection.Top:
                inRange = dy < thresholdY && dx < thresholdX;
                movingTowardDoor = inputDir.y > minInput;
                break;
            case DoorDirection.Bottom:
                inRange = dy < thresholdY && dx < thresholdX;
                movingTowardDoor = inputDir.y < -minInput;
                break;
            case DoorDirection.Left:
                inRange = dx < thresholdY && dy < thresholdX;
                movingTowardDoor = inputDir.x < -minInput;
                break;
            case DoorDirection.Right:
                inRange = dx < thresholdY && dy < thresholdX;
                movingTowardDoor = inputDir.x > minInput;
                break;
            default: return;
        }

        if (inRange && movingTowardDoor)
        {
            MapManager.Instance.OnPortalTriggered();
            MapManager.Instance.SwitchRoom(targetRoomId, direction);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 可破坏墙壁：检测子弹
        if (_breakable && other.TryGetComponent<Projectile>(out var proj))
        {
            OnBulletHit(proj);
            return;
        }
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
        _locked = false;

        // 隐藏墙破坏后显示门
        if (doorBlocker != null) doorBlocker.SetActive(false);
        if (doorSprite != null) doorSprite.enabled = true;
        if (openTrigger != null) openTrigger.enabled = true;

        Debug.Log($"RoomPortal: 隐藏墙已破坏, 门已显示 (方向: {direction})");
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
        if (_hidden) return;
        _locked = locked;

        if (openTrigger != null)
            openTrigger.enabled = !locked;

        if (doorSprite != null)
            doorSprite.enabled = !locked;

        if (doorBlocker != null)
            doorBlocker.SetActive(locked);
    }

    /// <summary>永久隐藏门（无连接房间），不显示墙壁贴图</summary>
    public void HideAsWall()
    {
        targetRoomId = -1;
        _hidden = true;
        _breakable = false;
        _hiddenWallHP = 0;

        if (openTrigger != null) openTrigger.enabled = false;
        if (doorSprite != null) doorSprite.enabled = false;
        if (doorBlocker != null) doorBlocker.SetActive(false);
    }
}
