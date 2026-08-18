using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 随从行为组件：接管被同化敌人的 AI，跟随玩家 + 搜索并攻击其他敌人。
/// 由 EnemyCore.Assimilate() 激活。
/// 攻击方式：碰撞伤害（EnemyCore.ProcessContactDamage）+ 技能。
/// </summary>
[RequireComponent(typeof(EnemyCore))]
public class EnemyFollower : MonoBehaviour
{
    [Header("随从参数")]
    [SerializeField] private float _followDistance = 3.5f;
    [SerializeField] private float _detectionRange = 8f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _followSpeed = 3f;
    [Tooltip("离玩家超过此距离时直接传送到身边，防止掉队")]
    [SerializeField] private float _maxTeleportDistance = 15f;

    [Header("复活")]
    [Tooltip("死亡后复活延迟（秒）")]
    [SerializeField] private float _respawnDelay = 10f;

    private Transform _player;
    private EnemyCore _core;
    private EnemyMovement _movement;
    private EnemySkillManager _skillManager;
    private bool _isActive;
    private bool _isReviving;

    // 进化倾向 buff：记录原始属性，动态刷新时从原始值重算
    private float _origMaxHealth, _origPatrolSpeed, _origChaseSpeed;
    private float _origDetectionRange, _origAttackRange, _origContactDamage, _origContactDamageCooldown;

    /// <summary>所有活跃随从列表（供 MapManager 查询，避免 FindObjectsByType）</summary>
    public static readonly System.Collections.Generic.List<EnemyFollower> ActiveFollowers = new();

    // ── 道具增强（静态，影响所有随从）──
    /// <summary>随从生命上限乘区（免疫球蛋白）</summary>
    public static float MaxHealthMultiplier = 1f;
    /// <summary>随从伤害乘区（集落刺激因子 CSF）</summary>
    public static float DamageMultiplier = 1f;
    /// <summary>随从技能冷却乘区（白细胞介素-2 IL-2）</summary>
    public static float FollowerCooldownFactor = 1f;
    /// <summary>随从攻击是否附带减速（干扰素）</summary>
    public static bool ApplySlow = false;
    /// <summary>减速比例（干扰素），0.8 = 减速20%</summary>
    public static float SlowFactor = 0.8f;
    /// <summary>减速持续时间（秒）</summary>
    public static float SlowDuration = 2f;
    /// <summary>随从数量上限（胸腺肽），默认4</summary>
    public static int MaxFollowerCount = 4;

    private void OnEnable()
    {
        ActiveFollowers.Add(this);

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted += OnRoomSwitchStarted;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ActiveFollowers.Remove(this);
        if (_core != null && _core.Health != null)
            _core.Health.OnDied -= HandleDeath;
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted -= OnRoomSwitchStarted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
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

        // 先脱离房间父节点，再标记 DontDestroyOnLoad（对有父节点的子对象直接调用不生效）
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>根据玩家进化倾向刷新随从全属性增幅（叠加道具乘区）</summary>
    public void ApplyEvolutionBuff()
    {
        if (_core?.Health == null || !_isActive) return;

        var playerStats = _player?.GetComponent<PlayerStats>();
        if (playerStats == null) return;

        float tendency = playerStats.EvolutionTendency;
        float factor = playerStats.EvolveFollowerBuffFactor;
        float mult = Mathf.Max(0f, 1f + (-tendency) * factor);

        _core.Health.MaxHealth = Mathf.Max(_origMaxHealth * mult * MaxHealthMultiplier, 1f);
        _core.Health.PatrolSpeed = _origPatrolSpeed * mult;
        _core.Health.ChaseSpeed = _origChaseSpeed * mult;
        _core.Health.DetectionRange = _origDetectionRange * mult;
        _core.Health.AttackRange = _origAttackRange * mult;
        _core.Health.ContactDamage = _origContactDamage * mult * DamageMultiplier;
        _core.Health.ContactDamageCooldown = _origContactDamageCooldown * mult;

        // 应用随从技能冷却乘区
        if (_skillManager != null)
        {
            foreach (var s in _skillManager.SkillInstances)
                s.CooldownFactor = FollowerCooldownFactor;
        }
    }

    /// <summary>刷新所有活跃随从的进化倾向 buff（由 PlayerStats 在倾向值变化时调用）</summary>
    public static void RefreshAllFollowers(float tendency, float factor)
    {
        foreach (var f in ActiveFollowers)
            f.ApplyEvolutionBuff();
    }

    /// <summary>刷新所有活跃随从（道具增强变化时调用）</summary>
    public static void RefreshAllFollowers()
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
        // 随从传送由 MapManager.TeleportFollowers 统一处理（在玩家传送到新房间之后）
    }

    private void HandleDeath()
    {
        _isActive = false;
        _isReviving = true;

        // 禁用碰撞
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        _movement?.Stop();

        // 隐藏视觉
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        // 复活
        if (_core?.Health != null)
        {
            _core.Health.Revive();
            ApplyEvolutionBuff();
        }

        // 传送到玩家身边
        if (_player != null)
            _movement?.Teleport(_player.position);

        // 恢复视觉和碰撞
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = true;
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = true;

        _isActive = true;
        _isReviving = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Start" || scene.name == "Result" || scene.name == "Winning")
        {
            Destroy(gameObject);
            return;
        }

        // 游戏场景：重新查找玩家引用
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
            _player = PlayerManager.Instance.CurrentPlayer.transform;
        else
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // 传送到玩家身边（防止随从出现在旧位置）
        if (_player != null && _movement != null && !_isReviving)
            _movement.Teleport(_player.position);
    }
}
