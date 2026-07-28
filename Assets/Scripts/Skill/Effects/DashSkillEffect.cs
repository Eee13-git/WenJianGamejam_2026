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

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        MonoBehaviour mono = caster.CasterTransform.GetComponent<MonoBehaviour>();
        if (mono == null) return;

        mono.StartCoroutine(DashRoutine(caster, direction));
    }

    private System.Collections.IEnumerator DashRoutine(ISkillCaster caster, Vector2 dir)
    {
        Transform t = caster.CasterTransform;
        Vector3 start = t.position;
        Vector3 end = start + (Vector3)(dir * distance);
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
