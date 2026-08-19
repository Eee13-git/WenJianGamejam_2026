using UnityEngine;

[CreateAssetMenu(fileName = "CodexEnemyEntry", menuName = "Game/Codex/Enemy Entry")]
public class CodexEnemyEntrySO : CodexEntrySO
{
    [Tooltip("敌人配置")]
    public EnemyConfig enemyConfig;

    [Tooltip("敌人预制体")]
    public GameObject enemyPrefab;

    [Tooltip("图标（从预制体 SpriteRenderer 截取或手动指定）")]
    public Sprite icon;
}
