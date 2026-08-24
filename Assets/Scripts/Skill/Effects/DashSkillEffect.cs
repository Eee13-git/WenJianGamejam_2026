using UnityEngine;

/// <summary>
/// 冲刺技能效果：施法者向目标方向瞬移一段距离。
/// 右键 -> Create -> Game -> Skill Effect -> Dash
/// </summary>
[CreateAssetMenu(fileName = "DashEffect", menuName = "Game/Skill Effect/Dash")]
public class DashSkillEffect : SkillEffectBase
{
    [Header("冲刺配置")]
    [Tooltip("冲刺距离")]
    public float distance = 5f;

    [Tooltip("冲刺时间（秒），0 = 瞬间）")]
    public float duration = 0.15f;

    [Tooltip("冲刺过程中是否无敌")]
    public bool invincibleDuringDash = true;

    [Tooltip("冲刺拖影特效预制体（可选）")]
    public GameObject trailVfx;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外冲刺距离（0=不成长）")]
    public float distancePerLevel = 0f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType, int level)
    {
        MonoBehaviour mono = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (mono == null) return;

        // 机制成长：冲刺距离随等级提升
        float actualDistance = distance + (level - 1) * distancePerLevel;

        mono.StartCoroutine(DashRoutine(caster, direction, actualDistance));
    }

    private System.Collections.IEnumerator DashRoutine(ISkillCaster caster, Vector2 dir, float actualDistance)
    {
        Transform t = caster.CasterTransform;
        Vector3 start = t.position;
        Vector3 end = start + (Vector3)(dir * actualDistance);
        float elapsed = 0f;

        if (trailVfx != null)
            Instantiate(trailVfx, t.position, Quaternion.identity, t);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tVal = elapsed / duration;
            t.position = Vector3.Lerp(start, end, tVal);
            yield return null;
        }

        t.position = end;
    }
}
