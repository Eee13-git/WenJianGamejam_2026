using UnityEngine;

/// <summary>
/// 吗啡分裂体 — 围绕玩家轨道运行，跟随本体攻击方向射击。
/// 伤害/弹速/射速读取本体 PlayerStats（享受本体藏品加成）。
/// 实现 IDamageable 供敌方投射物命中判定，受击即死。
/// </summary>
public class MorphineClone : MonoBehaviour, IDamageable
{
    private Transform _player;
    private PlayerStats _playerStats;
    private GameObject _bulletPrefab;
    private PlayerCombat _playerCombat;
    private Rigidbody2D _rb;
    private float _orbitRadius;
    private float _orbitSpeed;
    private float _orbitAngle;
    private bool _isDead;

    /// <summary>分裂体是否已死亡</summary>
    public bool IsDead => _isDead;

    /// <summary>分裂体死亡回调，由 MorphineCloneBuff 订阅以重新分配相位</summary>
    public event System.Action OnCloneDied;

    /// <summary>
    /// 初始化分裂体。
    /// </summary>
    public void Initialize(
        Transform player,
        PlayerStats stats,
        GameObject bulletPrefab,
        PlayerCombat combat,
        float orbitRadius,
        float orbitSpeed,
        int index)
    {
        _player = player;
        _playerStats = stats;
        _bulletPrefab = bulletPrefab;
        _playerCombat = combat;
        _orbitRadius = orbitRadius;
        _orbitSpeed = orbitSpeed;
        _orbitAngle = index * 360f;

        _rb = GetComponent<Rigidbody2D>();

        // 订阅本体射击事件
        if (_playerCombat != null)
            _playerCombat.OnShoot += OnPlayerShoot;

        // 订阅房间切换
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted += OnRoomSwitchStarted;
    }

    /// <summary>更新轨道相位（角度），用于分裂体增减时重新均分</summary>
    public void SetOrbitAngle(float angle)
    {
        _orbitAngle = angle;
    }

    private void Update()
    {
        if (_isDead || _player == null) return;

        // 轨道运行
        _orbitAngle += _orbitSpeed * Time.deltaTime;
        float rad = _orbitAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * _orbitRadius;
        Vector3 targetPos = _player.position + offset;

        // 用 MovePosition 确保 trigger 碰撞检测正常
        if (_rb != null)
            _rb.MovePosition(targetPos);
        else
            transform.position = targetPos;
    }

    private void OnPlayerShoot(Vector2 direction, float bulletSpeed, float attackStrength)
    {
        if (_isDead || _bulletPrefab == null || _playerStats == null) return;

        // 沿本体射击方向发射子弹
        GameObject bullet = ProjectilePool.Get(_bulletPrefab, transform.position, Quaternion.identity);
        var proj = bullet.GetComponent<Projectile>();
        if (proj != null)
            proj.Initialize(direction, bulletSpeed, attackStrength,
                Projectile.OwnerType.Player, _player.gameObject);
    }

    private void OnRoomSwitchStarted(int fromRoomId, int toRoomId)
    {
        if (_isDead || _player == null) return;

        if (MapManager.Instance != null)
        {
            RoomRoot targetRoom = MapManager.Instance.GetRoom(toRoomId);
            if (targetRoom != null)
                transform.SetParent(targetRoom.transform, true);
        }
    }

    // ---------- IDamageable ----------

    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        Die();
    }

    private void Die()
    {
        _isDead = true;

        if (_playerCombat != null)
            _playerCombat.OnShoot -= OnPlayerShoot;

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted -= OnRoomSwitchStarted;

        foreach (var col in GetComponents<Collider2D>())
            col.enabled = false;

        OnCloneDied?.Invoke();

        Destroy(gameObject, 0.3f);
    }

    private void OnDestroy()
    {
        if (_playerCombat != null)
            _playerCombat.OnShoot -= OnPlayerShoot;

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted -= OnRoomSwitchStarted;
    }
}
