using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 核心 — 挂在有 EnemyCore 的 GameObject 上，管理阶段切换和技能调度。
/// 不修改任何现有文件。
/// skillLibrary 取自 EnemyConfig.skillLibrary，无需在 BossCore 上重复配置。
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
    private EnemyHealth _health;
    private EnemyMovement _movement;
    private SkillLibrary _skillLibrary;
    private List<SkillInstance> _skillInstances = new();
    private BossPhaseData CurrentPhase => _phases != null && _currentPhaseIndex < _phases.Length
        ? _phases[_currentPhaseIndex] : null;

    private void Awake()
    {
        _enemyCore = GetComponent<EnemyCore>();
        _health = GetComponent<EnemyHealth>();
        _movement = GetComponent<EnemyMovement>();

        if (_phases == null || _phases.Length == 0)
        {
            Debug.LogWarning("BossCore: 未配置 _phases，Boss 将无法切换阶段");
            return;
        }
    }

    private void Start()
    {
        // 从 EnemyConfig 获取 SkillLibrary（无需在 BossCore 上冗余配置）
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
        TickAllCooldowns(dt);
        TryCastSkill();
    }

    // ==================== 阶段切换 ====================

    private void OnHealthChanged(float currentHp, float maxHp)
    {
        float ratio = maxHp > 0 ? currentHp / maxHp : 1f;

        // 从后往前找第一个阈值未触发的阶段
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
        var oldPhase = CurrentPhase;
        _currentPhaseIndex = phaseIndex;
        var newPhase = CurrentPhase;
        if (newPhase == null) return;

        Debug.Log($"BossCore: 进入 {newPhase.phaseName}");

        LoadPhaseSkills(phaseIndex);

        // 更新属性
        if (_movement != null)
        {
            float baseSpeed = _enemyCore.config != null ? _enemyCore.config.chaseSpeed : _movement.MoveSpeed;
            _movement.MoveSpeed = baseSpeed * newPhase.moveSpeedMult;
        }

        _nextSkillTime = Time.time + 0.5f; // 短暂延迟后开始放技能
    }

    // ==================== 技能管理 ====================

    private void LoadPhaseSkills(int phaseIndex)
    {
        _skillInstances.Clear();
        if (_phases == null || phaseIndex >= _phases.Length) return;
        var phase = _phases[phaseIndex];
        if (phase.skills == null || _skillLibrary == null) return;

        foreach (var sd in phase.skills)
        {
            if (sd == null) continue;
            var inst = _skillLibrary.CreateSkillInstance(sd.skillId);
            if (inst != null) _skillInstances.Add(inst);
        }
    }

    private void TickAllCooldowns(float dt)
    {
        foreach (var s in _skillInstances)
            s.TickCooldown(dt);
    }

    private void TryCastSkill()
    {
        var phase = CurrentPhase;
        if (phase == null || _skillInstances.Count == 0) return;
        if (Time.time < _nextSkillTime) return;

        // 找一个冷却完毕的技能
        var ready = new List<SkillInstance>();
        foreach (var s in _skillInstances)
        {
            if (!s.IsCoolingDown) ready.Add(s);
        }
        if (ready.Count == 0) return;

        var chosen = ready[Random.Range(0, ready.Count)];
        Vector2 dir = _enemyCore.PlayerTarget != null
            ? (_enemyCore.PlayerTarget.position - transform.position).normalized
            : Vector2.down;

        chosen.TryCast(_enemyCore, dir);
        _nextSkillTime = Time.time + phase.skillInterval;
    }
}
