using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 核心 — 挂在有 EnemyCore 的 GameObject 上，管理阶段切换和技能调度。
/// 使用 EnemySkillManager 管理技能，不自行维护技能列表。
/// skillLibrary 取自 EnemyConfig.skillLibrary。
/// </summary>
[RequireComponent(typeof(EnemyCore))]
public class BossCore : MonoBehaviour
{
    [Header("阶段配置")]
    [Tooltip("按健康阈值升序排列的阶段 (Phase1 在前, Phase2 在后)")]
    [SerializeField] private BossPhaseData[] _phases;

    [Header("Boss 血条")]
    [Tooltip("Boss 血条 prefab（若场景中无 BossHealthBar 实例则自动创建）")]
    [SerializeField] private GameObject _bossHealthBarPrefab;

    [Tooltip("由状态机管理的技能索引（这些技能不在 BossCore.Update 中自动施放）")]
    [SerializeField] private int[] _stateManagedSkillIndices = new int[] { 0 };

    [Header("调试")]
    [SerializeField] private int _currentPhaseIndex;
    [SerializeField] private float _nextSkillTime;

    private EnemyCore _enemyCore;
    private EnemyStats _health;
    private EnemyMovement _movement;
    private EnemySkillManager _skillManager;
    private SkillLibrary _skillLibrary;
    private BossPhaseData CurrentPhase => _phases != null && _currentPhaseIndex < _phases.Length
        ? _phases[_currentPhaseIndex] : null;

    private void Awake()
    {
        _enemyCore = GetComponent<EnemyCore>();
        _health = GetComponent<EnemyStats>();
        _movement = GetComponent<EnemyMovement>();
        _skillManager = GetComponent<EnemySkillManager>();

        if (_phases == null || _phases.Length == 0)
        {
            Debug.LogWarning("BossCore: 未配置 _phases，Boss 将无法切换阶段");
            return;
        }
    }

    private void Start()
    {
        _skillLibrary = _enemyCore.config != null ? _enemyCore.config.skillLibrary : null;
        if (_skillLibrary == null)
            Debug.LogWarning("BossCore: EnemyConfig.skillLibrary 未配置");

        LoadPhaseSkills(0);

        // 确保Boss血条存在：若场景中无 BossHealthBar.Instance，自动从 prefab 实例化
        if (BossHealthBar.Instance == null && _bossHealthBarPrefab != null)
        {
            var barGO = Instantiate(_bossHealthBarPrefab);
            barGO.name = "BossHealthBar";
            DontDestroyOnLoad(barGO);
        }

        // Start 在所有 Awake 之后运行，此时 EnemyCore.Awake 已完成 EnemyStats 初始化
        if (_health == null)
            _health = GetComponent<EnemyStats>();

        // 显示 Boss 血条 — 直接传递 EnemyStats 引用，BossHealthBar 每帧轮询（与 PlayerStatsPanel 同一 MVC 模式）
        if (BossHealthBar.Instance != null && _health != null)
            BossHealthBar.Instance.Show(_enemyCore.config != null ? _enemyCore.config.displayName : "Boss", _health);
    }

    private void OnEnable()
    {
        if (_health == null)
            _health = GetComponent<EnemyStats>();
        if (_health != null)
            _health.OnHealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHealthChanged -= OnHealthChanged;
    }

    private void OnDestroy()
    {
        // Boss 死亡/销毁 → 隐藏血条
        if (BossHealthBar.Instance != null)
            BossHealthBar.Instance.Hide();
    }

    private void Update()
    {
        if (_enemyCore.IsDead) return;

        float dt = Time.deltaTime;

        // Tick skill cooldowns via EnemySkillManager
        if (_skillManager != null)
            _skillManager.TickCooldowns(dt);

        TryCastSkill();
    }

    // ==================== 阶段切换 ====================

    private void OnHealthChanged(float currentHp, float maxHp)
    {
        // 阶段切换检测（血条更新由 BossHealthBar.Update 轮询处理）
        float ratio = maxHp > 0 ? currentHp / maxHp : 1f;

        for (int i = _phases.Length - 1; i > _currentPhaseIndex; i--)
        {
            if (ratio <= _phases[i].healthThreshold)
            {
                EnterPhase(i);
                return;
            }
        }
    }

    private void EnterPhase(int phaseIndex)
    {
        _currentPhaseIndex = phaseIndex;
        var newPhase = CurrentPhase;
        if (newPhase == null) return;

        Debug.Log($"BossCore: 进入 {newPhase.phaseName}");

        LoadPhaseSkills(phaseIndex);

        if (_health != null)
        {
            float baseSpeed = _health.ChaseSpeed;
            _health.ChaseSpeed = baseSpeed * newPhase.moveSpeedMult;
        }

        _nextSkillTime = Time.time + 0.5f;
    }

    // ==================== 技能管理（通过 EnemySkillManager） ====================

    private void LoadPhaseSkills(int phaseIndex)
    {
        if (_phases == null || phaseIndex >= _phases.Length || _skillManager == null || _skillLibrary == null) return;
        var phase = _phases[phaseIndex];
        if (phase.skills == null) return;

        var ids = new List<string>();
        foreach (var sd in phase.skills)
            if (sd != null) ids.Add(sd.skillId);

        _skillManager.LoadSkills(_skillLibrary, ids.ToArray());
    }

    private bool IsStateManaged(int index)
    {
        if (_stateManagedSkillIndices == null) return false;
        foreach (var i in _stateManagedSkillIndices)
            if (i == index) return true;
        return false;
    }

    private void TryCastSkill()
    {
        var phase = CurrentPhase;
        if (phase == null || _skillManager == null) return;
        if (Time.time < _nextSkillTime) return;

        // 遍历技能，找第一个就绪且非状态机管理的技能（lunge 等由 BossLungeState 管理）
        var skills = _skillManager.SkillInstances;
        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            if (skill == null || skill.Data == null) continue;
            if (skill.Data.passive) continue;
            if (skill.IsCoolingDown) continue;
            if (IsStateManaged(i)) continue;

            // 使用 EnemySkillManager.TryCastSkill 确保动画+延迟+方向正确传递
            Vector2 dir = _enemyCore.PlayerTarget != null
                ? (_enemyCore.PlayerTarget.position - transform.position).normalized
                : Vector2.down;

            if (_skillManager.TryCastSkill(i, _enemyCore, dir))
            {
                _nextSkillTime = Time.time + phase.skillInterval;
                return;
            }
        }
    }
}
