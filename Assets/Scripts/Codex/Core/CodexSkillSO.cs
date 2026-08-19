using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CodexSkill", menuName = "Game/Codex/Skill Codex")]
public class CodexSkillSO : CodexSO
{
    [SerializeField] private List<CodexSkillEntrySO> _entries = new();

    public IReadOnlyList<CodexSkillEntrySO> SkillEntries => _entries;

    public override IReadOnlyList<CodexEntrySO> Entries =>
        _entries.Cast<CodexEntrySO>().ToList();
}
