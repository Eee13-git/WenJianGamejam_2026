using UnityEngine;

/// <summary>
/// Boss 阶段配置 — 定义单个阶段的技能列表和行为参数。
/// </summary>
[CreateAssetMenu(fileName = "BossPhaseData", menuName = "Boss/Phase Data")]
public class BossPhaseData : ScriptableObject
{
    [Header("阶段信息")]
    [Tooltip("阶段名称，如\"一阶段\"")]
    public string phaseName = "Phase 1";

    [Header("技能")]
    [Tooltip("该阶段可使用的技能数据 (SkillData)")]
    public SkillData[] skills;

    [Tooltip("技能释放间隔 (秒)")]
    public float skillInterval = 4f;

    [Header("属性加成")]
    [Tooltip("移速乘数")]
    [Range(0.5f, 2f)]
    public float moveSpeedMult = 1f;

    [Tooltip("攻击间隔乘数 (<1=更快)")]
    [Range(0.3f, 2f)]
    public float attackIntervalMult = 1f;

    [Header("总血量阈值 (0~1)")]
    [Tooltip("低于此比例触发该阶段 (如 Phase2=0.5)")]
    [Range(0f, 1f)]
    public float healthThreshold = 1f;
}
