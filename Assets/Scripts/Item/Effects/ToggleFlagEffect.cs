using UnityEngine;

/// <summary>
/// 布尔标记道具效果 — 通过 Inspector 选择要开关的标记，获取时开启、移除时关闭。
/// 可复用于：灵体子弹、碰撞免疫等布尔型效果。
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
        BulletScale
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
                Projectile.BulletScaleMultiplier = _floatValue;
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
                Projectile.BulletScaleMultiplier = 1f;
                break;
        }
    }
}
