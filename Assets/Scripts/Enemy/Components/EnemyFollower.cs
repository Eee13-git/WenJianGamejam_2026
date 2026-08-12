using UnityEngine;

/// <summary>
/// 随从行为组件：接管被同化敌人的 AI，跟随玩家 + 搜索并攻击其他敌人。
/// 由 EnemyCore.Assimilate() 激活。
/// 攻击方式：碰撞伤害（EnemyCore.ProcessContactDamage）+ 技能。
/// </summary>
[RequireComponent(typeof(EnemyCore))]
public class EnemyFollower : MonoBehaviour
{
    [Header("随从参数")]
    [SerializeField] private float _followDistance = 2f;
    [SerializeField] private float _detectionRange = 8f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _followSpeed = 3f;
    [Tooltip("离玩家超过此距离时直接传送到身边，防止掉队")]
    [SerializeField] private float _maxTeleportDistance = 12f;

    private Transform _player;
    private EnemyCore _core;
    private EnemyMovement _movement;
    private EnemySkillManager _skillManager;
    private bool _isActive;

    // 进化倾向 buff：记录原始属性，动态刷新时从原始值重算
    private float _origMaxHealth, _origPatrolSpeed, _origChaseSpeed;
    private float _origDetectionRange, _origAttackRange, _origContactDamage, _origContactDamageCooldown;

    /// <summary>所有活跃随从列表（供 MapManager 查询，避免 FindObjectsByType）</summary>
    public static readonly System.Collections.Generic.List<EnemyFollower> ActiveFollowers = new();

    private void OnEnable()
    {
        ActiveFollowers.Add(this);

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted += OnRoomSwitchStarted;
    }

    private void OnDisable()
    {
        ActiveFollowers.Remove(this);
        if (_core != null && _core.Health != null)
            _core.Health.OnDied -= HandleDeath;
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted -= OnRoomSwitchStarted;
    }

    private void Awake()
    {
        _core = GetComponent<EnemyCore>();
    }

    /// <summary>
    /// 激活随从模式。
    /// </summary>
    public void Activate(Transform player)
    {
        _player = player;
        _isActive = true;

        _movement = _core.Movement;
        _skillManager = _core.SkillManager;

        // 记录原始属性，用于进化倾向 buff 重算
        if (_core.Health != null)
        {
            _origMaxHealth = _core.Health.MaxHealth;
            _origPatrolSpeed = _core.Health.PatrolSpeed;
            _origChaseSpeed = _core.Health.ChaseSpeed;
            _origDetectionRange = _core.Health.DetectionRange;
            _origAttackRange = _core.Health.AttackRange;
            _origContactDamage = _core.Health.ContactDamage;
            _origContactDamageCooldown = _core.Health.ContactDamageCooldown;

            // 应用当前进化倾向 buff
            ApplyEvolutionBuff();

            _core.Health.OnDied += HandleDeath;
        }
    }

    /// <summary>根据玩家进化倾向刷新随从全属性增幅</summary>
    public void ApplyEvolutionBuff()
    {
        if (_core?.Health == null || !_isActive) return;

        var playerStats = _player?.GetComponent<PlayerStats>();
        if (playerStats == null) return;

        float tendency = playerStats.EvolutionTendency;
        float factor = playerStats.EvolveFollowerBuffFactor;
        float mult = Mathf.Max(0f, 1f + (-tendency) * factor);

        _core.Health.MaxHealth = Mathf.Max(_origMaxHealth * mult, 1f);
        _core.Health.PatrolSpeed = _origPatrolSpeed * mult;
        _core.Health.ChaseSpeed = _origChaseSpeed * mult;
        _core.Health.DetectionRange = _origDetectionRange * mult;
        _core.Health.AttackRange = _origAttackRange * mult;
        _core.Health.ContactDamage = _origContactDamage * mult;
        _core.Health.ContactDamageCooldown = _origContactDamageCooldown * mult;
    }

    /// <summary>刷新所有活跃随从的进化倾向 buff（由 PlayerStats 在倾向值变化时调用）</summary>
    public static void RefreshAllFollowers(float tendency, float factor)
    {
        foreach (var f in ActiveFollowers)
            f.ApplyEvolutionBuff();
    }

    private void Update()
    {
        if (!_isActive || _player == null) return;
        if (_core.Health != null && _core.Health.IsDead) return;

        Vector2 pos = transform.position;
        Vector2 playerPos = _player.position;

        // 离玩家过远 → 直接传送到身边，防止掉队或被卡住
        float sqrDistToPlayer = (pos - playerPos).sqrMagnitude;
        if (sqrDistToPlayer > _maxTeleportDistance * _maxTeleportDistance)
        {
            _movement?.Teleport(playerPos);
            return;
        }

        // 寻找最近的敌人
        Transform nearestEnemy = FindNearestEnemy(pos);

        if (nearestEnemy != null)
        {
            Vector2 enemyPos = nearestEnemy.position;
            float distToEnemy = Vector2.Distance(pos, enemyPos);

            if (distToEnemy <= _attackRange)
            {
                _movement?.Stop();

                // 尝试释放技能
                if (_skillManager != null && _skillManager.SkillInstances.Count > 0)
                {
                    Vector2 dir = (enemyPos - pos).normalized;
                    _skillManager.TryCastSkill(0, _core, dir);
                }
            }
            else
            {
                _movement?.MoveTowardsPosition(enemyPos, _followSpeed);
            }
            return;
        }

        // 没有敌人 → 跟随玩家
        float distToPlayer = Vector2.Distance(pos, playerPos);
        if (distToPlayer > _followDistance)
        {
            _movement?.MoveTowardsPosition(playerPos, _followSpeed);
        }
        else
        {
            _movement?.Stop();
        }
    }

    private Transform FindNearestEnemy(Vector2 center)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, _detectionRange);
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            if (_player != null && hit.transform == _player) continue;

            float d = (hit.transform.position - (Vector3)center).sqrMagnitude;
            if (d < minDist)
            {
                minDist = d;
                nearest = hit.transform;
            }
        }

        return nearest;
    }

    private void OnRoomSwitchStarted(int fromRoomId, int toRoomId)
    {
        if (!_isActive || _player == null) return;

        // 1. 将随从切换到新房间的父节点，防止旧房间禁用时连带禁用它
        if (MapManager.Instance != null)
        {
            RoomRoot targetRoom = MapManager.Instance.GetRoom(toRoomId);
            if (targetRoom != null)
                transform.SetParent(targetRoom.transform, true);
        }

        // 2. 立即传送到玩家身边
        _movement?.Teleport(_player.position);
    }

    private void HandleDeath()
    {
        _isActive = false;

        // 禁用碰撞
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        _movement?.Stop();
        Destroy(gameObject, 0.5f);
    }
}
