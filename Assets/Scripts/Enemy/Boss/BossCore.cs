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
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnHealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHealthChanged -= OnHealthChanged;
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

    private void TryCastSkill()
    {
        var phase = CurrentPhase;
        if (phase == null || _skillManager == null) return;
        if (Time.time < _nextSkillTime) return;

        var ready = _skillManager.GetReadySkill();
        if (ready == null) return;

        Vector2 dir = _enemyCore.PlayerTarget != null
            ? (_enemyCore.PlayerTarget.position - transform.position).normalized
            : Vector2.down;

        ready.TryCast(_enemyCore, dir);
        _nextSkillTime = Time.time + phase.skillInterval;
    }
}
