using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CodexItem", menuName = "Game/Codex/Item Codex")]
public class CodexItemSO : CodexSO
{
    [SerializeField] private List<CodexItemEntrySO> _entries = new();

    public IReadOnlyList<CodexItemEntrySO> ItemEntries => _entries;

    public override IReadOnlyList<CodexEntrySO> Entries =>
        _entries.Cast<CodexEntrySO>().ToList();
}
