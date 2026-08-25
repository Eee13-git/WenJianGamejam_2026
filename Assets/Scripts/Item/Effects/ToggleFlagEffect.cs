using UnityEngine;

/// <summary>
/// 布尔/数值标记道具效果 — 通过 Inspector 选择要开关的标记，获取时开启、移除时关闭。
/// 可复用于：灵体子弹、碰撞免疫、随从增强、技能CD、地图点亮等。
/// </summary>
[CreateAssetMenu(fileName = "ToggleFlagEffect", menuName = "Game/Item Effect/Toggle Flag")]
public class ToggleFlagEffect : ItemEffectBase
{
    /// <summary>可开关的标记类型</summary>
    public enum FlagType
    {
        /// <summary>灵体子弹：玩家方子弹穿透障碍物</summary>
        SpiritBullet,
        /// <summary>碰撞免疫：免疫敌人碰撞伤害</summary>
        ContactDamageImmunity,
        /// <summary>跟踪子弹：玩家方子弹追踪最近敌人</summary>
        HomingBullet,
        /// <summary>ATP获取效率提升</summary>
        AtpGainBoost,
        /// <summary>子弹大小倍率</summary>
        BulletScale,
        /// <summary>地图全点亮（荧光蛋白 GFP）</summary>
        RevealMap,
        /// <summary>技能释放概率跳过冷却（钙调蛋白）</summary>
        CooldownSkip,
        /// <summary>角色技能伤害乘区（cAMP）</summary>
        SkillDamageBonus,
        /// <summary>技能命中附带当前生命百分比伤害（蛋白激酶）</summary>
        SkillPercentDamage,
        /// <summary>玩家碰撞伤害减免乘区（细胞骨架）</summary>
        CollisionReduction,
        /// <summary>吞噬/同化倾向变化削减（吞噬体）</summary>
        EvolveReduction,
        /// <summary>随从生命上限乘区（免疫球蛋白）</summary>
        FollowerMaxHealth,
        /// <summary>随从伤害乘区（集落刺激因子 CSF）</summary>
        FollowerDamage,
        /// <summary>随从技能冷却乘区（白细胞介素-2 IL-2）</summary>
        FollowerCooldown,
        /// <summary>随从数量上限增加（胸腺肽）</summary>
        FollowerCount,
        /// <summary>随从攻击附带减速（干扰素）</summary>
        FollowerSlow
    }

    [SerializeField] private FlagType _flag;
    [SerializeField] private float _floatValue = 1.5f;

    public override void OnAcquire(GameObject owner)
    {
        switch (_flag)
        {
            case FlagType.SpiritBullet:
                Projectile.SpiritBulletMode = true;
                break;
            case FlagType.ContactDamageImmunity:
                var stats = owner.GetComponent<PlayerStats>();
                if (stats != null)
                    stats.ImmuneToContactDamage = true;
                break;
            case FlagType.HomingBullet:
                Projectile.HomingMode = true;
                break;
            case FlagType.AtpGainBoost:
                CurrencyManager.GainMultiplier = _floatValue;
                break;
            case FlagType.BulletScale:
                Projectile.BulletScaleMultiplier *= _floatValue;
                break;
            case FlagType.RevealMap:
                MinimapUI.RevealAllMap = true;
                var minimap = FindObjectOfType<MinimapUI>();
                minimap?.Refresh();
                break;
            case FlagType.CooldownSkip:
                SkillInstance.CooldownSkipChance = _floatValue;
                break;
            case FlagType.SkillDamageBonus:
                PlayerSkillManager.SkillDamageBonusMultiplier *= _floatValue;
                break;
            case FlagType.SkillPercentDamage:
                EnemyStats.OnSkillHitPercentDamage = _floatValue;
                break;
            case FlagType.CollisionReduction:
                EnemyCore.PlayerCollisionReductionFactor = _floatValue;
                break;
            case FlagType.EvolveReduction:
                ErodeChoicePopupManager.EvolveDeltaReduction = _floatValue;
                break;
            case FlagType.FollowerMaxHealth:
                EnemyFollower.MaxHealthMultiplier = _floatValue;
                EnemyFollower.RefreshAllFollowers();
                break;
            case FlagType.FollowerDamage:
                EnemyFollower.DamageMultiplier = _floatValue;
                EnemyFollower.RefreshAllFollowers();
                break;
            case FlagType.FollowerCooldown:
                EnemyFollower.FollowerCooldownFactor = _floatValue;
                EnemyFollower.RefreshAllFollowers();
                break;
            case FlagType.FollowerCount:
                EnemyFollower.MaxFollowerCount += Mathf.RoundToInt(_floatValue);
                break;
            case FlagType.FollowerSlow:
                EnemyFollower.ApplySlow = true;
                break;
        }
    }

    public override void OnRemove(GameObject owner)
    {
        switch (_flag)
        {
            case FlagType.SpiritBullet:
                Projectile.SpiritBulletMode = false;
                break;
            case FlagType.ContactDamageImmunity:
                var stats = owner.GetComponent<PlayerStats>();
                if (stats != null)
                    stats.ImmuneToContactDamage = false;
                break;
            case FlagType.HomingBullet:
                Projectile.HomingMode = false;
                break;
            case FlagType.AtpGainBoost:
                CurrencyManager.GainMultiplier = 1f;
                break;
            case FlagType.BulletScale:
                Projectile.BulletScaleMultiplier /= _floatValue;
                break;
            case FlagType.RevealMap:
                MinimapUI.RevealAllMap = false;
                var minimapOff = FindObjectOfType<MinimapUI>();
                minimapOff?.Refresh();
                break;
            case FlagType.CooldownSkip:
                SkillInstance.CooldownSkipChance = 0f;
                break;
            case FlagType.SkillDamageBonus:
                PlayerSkillManager.SkillDamageBonusMultiplier /= _floatValue;
                break;
            case FlagType.SkillPercentDamage:
                EnemyStats.OnSkillHitPercentDamage = 0f;
                break;
            case FlagType.CollisionReduction:
                EnemyCore.PlayerCollisionReductionFactor = 1f;
                break;
            case FlagType.EvolveReduction:
                ErodeChoicePopupManager.EvolveDeltaReduction = 0f;
                break;
            case FlagType.FollowerMaxHealth:
                EnemyFollower.MaxHealthMultiplier = 1f;
                EnemyFollower.RefreshAllFollowers();
                break;
            case FlagType.FollowerDamage:
                EnemyFollower.DamageMultiplier = 1f;
                EnemyFollower.RefreshAllFollowers();
                break;
            case FlagType.FollowerCooldown:
                EnemyFollower.FollowerCooldownFactor = 1f;
                EnemyFollower.RefreshAllFollowers();
                break;
            case FlagType.FollowerCount:
                EnemyFollower.MaxFollowerCount -= Mathf.RoundToInt(_floatValue);
                break;
            case FlagType.FollowerSlow:
                EnemyFollower.ApplySlow = false;
                break;
        }
    }
}
