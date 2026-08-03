using UnityEngine;

/// <summary>
/// Boss 召唤技能：在施法者周围随机位置生成小怪。
/// </summary>
[CreateAssetMenu(fileName = "Summon", menuName = "Game/Skill Effect/Summon")]
public class BossSkill_Summon : SkillEffectBase
{
    [Header("召唤")]
    [Tooltip("可召唤的敌人预制体池")]
    public GameObject[] enemyPrefabs;

    [Tooltip("召唤数量")]
    public int count = 3;

    [Tooltip("生成半径 (围绕施法者)")]
    public float spawnRadius = 4f;

    [Tooltip("生成间隔 (秒)")]
    public float spawnInterval = 0.3f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        MonoBehaviour mono = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (mono == null) return;
        mono.StartCoroutine(SummonRoutine(caster));
    }

    private System.Collections.IEnumerator SummonRoutine(ISkillCaster caster)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) yield break;

        Vector2 center = caster.CasterTransform.position;

        // 找到当前房间的 Transform 作为父节点
        Transform parent = caster.CasterTransform.parent;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector2 spawnPos = center + offset;

            GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            if (prefab == null) continue;

            var go = Object.Instantiate(prefab, spawnPos, Quaternion.identity, parent);
            go.name = $"Summon_{i}_{prefab.name}";

            yield return new WaitForSeconds(spawnInterval);
        }
    }
}
