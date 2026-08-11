using UnityEngine;

/// <summary>
/// 通用伤害免疫组件 — 挂在角色身上，授予一段时间的无敌（免疫所有伤害）。
/// 任何伤害入口（PlayerStats / EnemyStats 的 TakeDamage）都应检查 IsImmune。
/// 供"格挡后无敌帧"等机制复用。
/// </summary>
public class DamageImmunity : MonoBehaviour
{
    private float _remainingTime;

    /// <summary>当前是否免疫伤害</summary>
    public bool IsImmune => _remainingTime > 0f;

    /// <summary>剩余免疫时间（秒）</summary>
    public float RemainingTime => _remainingTime;

    /// <summary>授予无敌（刷新/覆盖现有免疫时长）</summary>
    public void GrantImmunity(float duration)
    {
        _remainingTime = Mathf.Max(_remainingTime, duration);
    }

    private void Update()
    {
        if (_remainingTime > 0f)
            _remainingTime -= Time.deltaTime;
    }
}
