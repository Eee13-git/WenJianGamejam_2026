using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性加成效果 (ScriptableObject) — 科技树效果的具体实现。
/// 配置 statName + bonus，ApplyBonus 时将加成累加到字典中。
/// 右键 -> Create -> TechTree -> Stat Bonus Effect 创建。
/// </summary>
[CreateAssetMenu(menuName = "TechTree/Stat Bonus Effect", fileName = "StatBonusEffect")]
public class StatBonusEffect : TechTreeEffectBase
{
    [Header("属性加成")]
    [Tooltip("加成的属性名: MaxHealth / MoveSpeed / AttackStrength / BulletSpeed / ShotsPerMinute / EvolutionTendency")]
    [SerializeField] private string _statName = "MaxHealth";

    [Tooltip("加成值（正数=增加，负数=减少）")]
    [SerializeField] private float _bonus = 0f;

    public string StatName => _statName;
    public float Bonus => _bonus;

    public override void ApplyBonus(Dictionary<string, float> bonuses)
    {
        if (bonuses.ContainsKey(_statName))
            bonuses[_statName] += _bonus;
        else
            bonuses[_statName] = _bonus;
    }

    public override string GetEffectSummary()
    {
        string statDisplay = _statName switch
        {
            "MaxHealth"      => "最大生命",
            "MoveSpeed"      => "移动速度",
            "AttackStrength" => "攻击力",
            "BulletSpeed"    => "子弹速度",
            "ShotsPerMinute" => "射速",
            _                => _statName
        };
        string sign = _bonus >= 0 ? "+" : "";
        return $"{sign}{_bonus} {statDisplay}";
    }
}
