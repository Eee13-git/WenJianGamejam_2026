using UnityEngine;

[CreateAssetMenu(fileName = "CodexSkillEntry", menuName = "Game/Codex/Skill Entry")]
public class CodexSkillEntrySO : CodexEntrySO
{
    [Tooltip("技能数据")]
    public SkillData skillData;
}
