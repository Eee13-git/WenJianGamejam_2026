using UnityEngine;

[CreateAssetMenu(fileName = "CodexItemEntry", menuName = "Game/Codex/Item Entry")]
public class CodexItemEntrySO : CodexEntrySO
{
    [Tooltip("道具数据")]
    public ItemData itemData;
}
