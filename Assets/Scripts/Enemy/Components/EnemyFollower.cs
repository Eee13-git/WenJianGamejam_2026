using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 随从行为组件：接管被同化敌人的 AI。
/// AI 决策由 FollowerStateMachine 驱动（Idle/Follow/Chase/Attack 四状态）。
/// 生命周期管理（传送/复活/跨场景/进化buff/道具增强）保留在本组件中。
/// 由 EnemyCore.Assimilate() 激活。
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

    [Header("同种升级")]
    [Tooltip("同种类随从升级：每级属性倍率增量（0.15 = 每级 +15%）")]
    [SerializeField] private float _levelStatFactor = 0.15f;
    [Tooltip("同种类随从升级：技能等级是否 +1")]
    [SerializeField] private bool _upgradeSkillsOnLevelUp = true;

    [Header("复活")]
    [Tooltip("死亡后复活延迟（秒）")]
    [SerializeField] private float _respawnDelay = 10f;

    private Transform _player;
    private EnemyCore _core;
    private EnemyMovement _movement;
    private EnemySkillManager _skillManager;
    private bool _isActive;
    private bool _isReviving;
    private bool _isPaused;
    private float _pauseStartTime;
    private const float MaxPauseDuration = 5f;
    private FollowerStateMachine _followerSM;

    // 进化倾向 buff：记录原始属性，动态刷新时从原始值重算
    private float _origMaxHealth, _origPatrolSpeed, _origChaseSpeed;
    private float _origDetectionRange, _origAttackRange, _origContactDamage, _origContactDamageCooldown;

    /// <summary>所有活跃随从列表（供 MapManager 查询，避免 FindObjectsByType）</summary>
    public static readonly System.Collections.Generic.List<EnemyFollower> ActiveFollowers = new();

    /// <summary>随从列表修订号：增删/升级时自增，UI 据此重建行</summary>
    public static int Revision { get; private set; }

    /// <summary>随从槽位索引（0..MaxFollowerCount-1，-1=未分配）</summary>
    public int SlotIndex { get; private set; } = -1;

    /// <summary>随从等级（同种升级叠加，默认 1）</summary>
    public int Level { get; private set; } = 1;

    // ── 道具增强（静态，影响所有随从）──
    /// <summary>随从生命上限乘区（免疫球蛋白）</summary>
    public static float MaxHealthMultiplier = 1f;
    /// <summary>随从伤害乘区（集落刺激因子 CSF）</summary>
    public static float DamageMultiplier = 1f;
    /// <summary>随从技能冷却乘区（白细胞介素-2 IL-2）</summary>
    public static float FollowerCooldownFactor = 1f;

    // ── 射速联动（进化倾向体系扩展）──
    // 随从不普攻，玩家的射速对随从无直接收益 → 将射速按比例映射为随从冷却缩短：
    //   冷却乘区 = 1 - max(0, 射速 - ShotsBasePerMinute) × ShotsToCooldownFactor，下限 MinCooldownFactor
    /// <summary>射速联动的基准射速（低于此值无冷却加成），默认=玩家初始射速 60/分</summary>
    public static float ShotsBasePerMinute = 60f;
    /// <summary>每单位射速的随从冷却乘区缩减（0.002 = 射速每 +50 → 随从冷却 -10%）</summary>
    public static float ShotsToCooldownFactor = 0.002f;
    /// <summary>随从冷却乘区下限（0.5 = 最多缩短 50%）</summary>
    public static float MinCooldownFactor = 0.5f;
    /// <summary>随从攻击是否附带减速（干扰素）</summary>
    public static bool ApplySlow = false;
    /// <summary>减速比例（干扰素），0.8 = 减速20%</summary>
    public static float SlowFactor = 0.8f;
    /// <summary>减速持续时间（秒）</summary>
    public static float SlowDuration = 2f;
    /// <summary>随从数量上限（胸腺肽），默认4</summary>
    public static int MaxFollowerCount = 4;

    // ── 暴露给状态机的只读属性 ──
    public EnemyCore Core => _core;
    public EnemyMovement Movement => _movement;
    public EnemySkillManager SkillManager => _skillManager;
    public Transform Player => _player;
    public FollowerStateMachine StateMachine => _followerSM;
    public float FollowDistance => _followDistance;
    public float DetectionRange => _detectionRange;
    public float AttackRange => _attackRange;
    public float FollowSpeed => _followSpeed;

    /// <summary>随从是否处于活跃状态（未被暂停）</summary>
    public bool IsActive => _isActive && !_isPaused;

    /// <summary>暂停/恢复随从 AI（不改变 _isActive，用于陷阱等临时暂停）</summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        if (paused)
        {
            _pauseStartTime = Time.time;
            _movement?.Stop();
        }
    }

    private void OnEnable()
    {
        ActiveFollowers.Add(this);
        Revision++;

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchStarted += OnRoomSwitchStarted;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ActiveFollowers.Remove(this);
        Revision++;
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
    /// <param name="player">玩家 Transform</param>
    /// <param name="slotIndex">槽位索引（-1=自动分配最小空闲槽）</param>
    public void Activate(Transform player, int slotIndex = -1)
    {
        _player = player;
        _isActive = true;

        // 槽位分配：显式指定或自动取最小空闲槽
        SlotIndex = slotIndex >= 0 ? slotIndex : FindFreeSlotIndex();

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

        // 创建随从状态机并启动
        _followerSM = new FollowerStateMachine();
        _followerSM.ChangeState(new FollowerIdleState(this));
    }

    /// <summary>
    /// 射速→随从冷却乘区：1 - max(0, 射速-基准) × 系数，clamp 到 [MinCooldownFactor, 1]。
    /// 玩家射速对随从无直接收益（随从不普攻），映射为随从技能冷却缩短。
    /// </summary>
    public static float GetShotsCooldownFactor(PlayerStats ps)
    {
        if (ps == null) return 1f;
        float factor = 1f - Mathf.Max(0f, ps.ShotsPerMinute - ShotsBasePerMinute) * ShotsToCooldownFactor;
        return Mathf.Clamp(factor, MinCooldownFactor, 1f);
    }

    /// <summary>根据玩家进化倾向刷新随从全属性增幅（叠加道具乘区 + 射速冷却联动）</summary>
    public void ApplyEvolutionBuff()
    {
        if (_core?.Health == null || !_isActive) return;

        var playerStats = _player?.GetComponent<PlayerStats>();
        if (playerStats == null) return;

        float tendency = playerStats.EvolutionTendency;
        float factor = playerStats.EvolveFollowerBuffFactor;
        float levelMult = 1f + (Level - 1) * _levelStatFactor;
        float mult = Mathf.Max(0f, 1f + (-tendency) * factor) * levelMult;

        _core.Health.MaxHealth = Mathf.Max(_origMaxHealth * mult * MaxHealthMultiplier, 1f);
        _core.Health.PatrolSpeed = _origPatrolSpeed * mult;
        _core.Health.ChaseSpeed = _origChaseSpeed * mult;
        _core.Health.DetectionRange = _origDetectionRange * mult;
        _core.Health.AttackRange = _origAttackRange * mult;
        _core.Health.ContactDamage = _origContactDamage * mult * DamageMultiplier;
        _core.Health.ContactDamageCooldown = _origContactDamageCooldown * mult;

        // 随从技能冷却乘区 = 道具 IL-2 乘区 × 射速联动（玩家射速越高随从冷却越短）
        if (_skillManager != null)
        {
            float cooldownFactor = FollowerCooldownFactor * GetShotsCooldownFactor(playerStats);
            foreach (var s in _skillManager.SkillInstances)
                s.CooldownFactor = cooldownFactor;
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

        // 安全超时：被陷阱暂停过久（协程被中断等）自动恢复
        if (_isPaused && Time.time - _pauseStartTime > MaxPauseDuration)
            SetPaused(false);

        if (_isPaused) return;

        // 离玩家过远 → 直接传送到身边，防止掉队或被卡住
        float sqrDistToPlayer = ((Vector2)transform.position - (Vector2)_player.position).sqrMagnitude;
        if (sqrDistToPlayer > _maxTeleportDistance * _maxTeleportDistance)
        {
            _movement?.Teleport(_player.position);
            return;
        }

        _followerSM?.Tick();
    }

    /// <summary>在检测范围内查找最近的 "Enemy" 标签目标</summary>
    public Transform FindNearestEnemy(Vector2 center)
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
        // 房间切换时恢复被陷阱暂停的随从（HoleTrap 协程已被中断，unlock 不会执行）
        if (_isPaused)
            SetPaused(false);
    }

    private void HandleDeath()
    {
        _isActive = false;
        _isReviving = true;

        // 停止状态机
        _followerSM?.ChangeState(null);

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

        // 重启状态机
        _followerSM?.ChangeState(new FollowerIdleState(this));
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

        // 更新寻路网格（场景重载后旧网格失效）
        if (_movement != null)
        {
            var roomMgr = UnityEngine.Object.FindObjectOfType<RoomManager>();
            if (roomMgr != null && roomMgr.roomRoot != null && roomMgr.roomRoot.PathGrid != null)
                _movement.SetRoomGrid(roomMgr.roomRoot.PathGrid);
        }

        // 传送到玩家身边（防止随从出现在旧位置）
        if (_player != null && _movement != null && !_isReviving)
            _movement.Teleport(_player.position);
    }

    // ── 槽位与升级 ──

    /// <summary>查找最小空闲槽位索引（0..MaxFollowerCount-1），满员返回 MaxFollowerCount</summary>
    private int FindFreeSlotIndex()
    {
        var used = new System.Collections.Generic.HashSet<int>();
        foreach (var f in ActiveFollowers)
        {
            if (f != this && f.SlotIndex >= 0)
                used.Add(f.SlotIndex);
        }

        for (int i = 0; i < MaxFollowerCount; i++)
        {
            if (!used.Contains(i))
                return i;
        }
        return MaxFollowerCount;
    }

    /// <summary>按槽位顺序返回随从列表（空槽为 null，长度 = MaxFollowerCount）</summary>
    public static System.Collections.Generic.List<EnemyFollower> GetSlotsInOrder()
    {
        var list = new System.Collections.Generic.List<EnemyFollower>();
        for (int i = 0; i < MaxFollowerCount; i++)
        {
            EnemyFollower found = null;
            foreach (var f in ActiveFollowers)
            {
                if (f.SlotIndex == i)
                {
                    found = f;
                    break;
                }
            }
            list.Add(found);
        }
        return list;
    }

    /// <summary>判断两个敌人是否同种类（config 引用相等，config 为空回退 displayName）</summary>
    public static bool SameType(EnemyCore a, EnemyCore b)
    {
        if (a == null || b == null) return false;

        var ca = a.config;
        var cb = b.config;
        if (ca != null && cb != null) return ca == cb;

        return string.Equals(ca != null ? ca.displayName : null,
                             cb != null ? cb.displayName : null);
    }

    /// <summary>
    /// 升级随从：等级 +1，属性按 _levelStatFactor 提升，技能等级 +1（可选）。
    /// </summary>
    public void UpgradeLevel()
    {
        Level++;

        if (_upgradeSkillsOnLevelUp && _skillManager != null)
        {
            foreach (var s in _skillManager.SkillInstances)
                s?.TryUpgrade();
        }

        ApplyEvolutionBuff();
        Revision++;
    }

    /// <summary>
    /// 把敌人同化到指定槽位：空槽=放置；同种=升级并消耗敌人；异种=替换（销毁旧随从）。
    /// 成功路径都会触发 OnAssimilated（供房间计数/门锁逻辑）。
    /// </summary>
    public static FollowerSlotResult AssimilateToSlot(EnemyCore enemy, Transform player, int slotIndex)
    {
        if (enemy == null || player == null)
            return FollowerSlotResult.Failed;

        var slots = GetSlotsInOrder();
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return FollowerSlotResult.Failed;

        var existing = slots[slotIndex];

        // 同种升级：提升现有随从等级，消耗被侵蚀的敌人（不创建新随从）
        if (existing != null && SameType(existing.Core, enemy))
        {
            existing.UpgradeLevel();
            enemy.ConsumeAsAssimilated(player);
            return FollowerSlotResult.Upgraded;
        }

        // 替换：同步销毁旧随从释放槽位（时间缩放 0 时 Destroy 延后一帧会导致槽位短暂重复）
        // 注意：DestroyImmediate 后 Unity 对象判空（existing != null）会变为 false，须先缓存布尔值
        bool hadExisting = existing != null;
        if (hadExisting)
            DestroyImmediate(existing.gameObject);

        // 放置/替换：同化敌人到指定槽位
        enemy.Assimilate(player, slotIndex);
        return hadExisting ? FollowerSlotResult.Replaced : FollowerSlotResult.Placed;
    }
}

/// <summary>同化到槽位的操作结果</summary>
public enum FollowerSlotResult
{
    Failed,
    Placed,
    Upgraded,
    Replaced
}
