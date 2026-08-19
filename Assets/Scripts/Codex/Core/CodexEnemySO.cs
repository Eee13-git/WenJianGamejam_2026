using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CodexEnemy", menuName = "Game/Codex/Enemy Codex")]
public class CodexEnemySO : CodexSO
{
    [SerializeField] private List<CodexEnemyEntrySO> _entries = new();

    public IReadOnlyList<CodexEnemyEntrySO> EnemyEntries => _entries;

    public override IReadOnlyList<CodexEntrySO> Entries =>
        _entries.Cast<CodexEntrySO>().ToList();
}
