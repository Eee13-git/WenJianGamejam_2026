using UnityEngine;

/// <summary>
/// 敌人/弹幕减速 Buff — 永久生效。
/// 获取氯丙嗪时挂载此 Buff，监听 OnAnyItemAcquired 检查三件套。
/// 三件套规则：一旦触发大幅减速，即使丢掉氯丙嗪或哌替啶仍保持；
/// 只有丢掉异丙嗪才恢复单件减速效果。
/// </summary>
[CreateAssetMenu(fileName = "ChlorpromazineSlowBuff", menuName = "Game/Buff Effect/Chlorpromazine Slow")]
public class ChlorpromazineSlowBuff : BuffEffectBase
{
    [Header("单件减速")]
    [SerializeField] private float _enemySlowFactor = 0.85f;
    [SerializeField] private float _bulletSlowFactor = 0.85f;

    [Header("三件套减速")]
    [SerializeField] private float _comboEnemySlowFactor = 0.4f;
    [SerializeField] private float _comboBulletSlowFactor = 0.4f;

    private GameObject _target;
    private bool _comboActivated;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _target = target;
        _comboActivated = false;

        ApplySingleSlow();
        ItemManager.OnAnyItemAcquired += OnAnyItemAcquired;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        ItemManager.OnAnyItemAcquired -= OnAnyItemAcquired;

        // 重置减速
        EnemyMovement.GlobalSpeedMultiplier = 1f;
        Projectile.GlobalSpeedMultiplier = 1f;
    }

    private void OnAnyItemAcquired(GameObject owner)
    {
        if (_target == null) return;
        var itemManager = owner.GetComponent<ItemManager>();
        if (itemManager == null) return;

        // 检查三件套
        bool hasCombo = itemManager.HasItem("chlorpromazine")
            && itemManager.HasItem("promethazine")
            && itemManager.HasItem("pethidine");

        if (hasCombo && !_comboActivated)
        {
            _comboActivated = true;
            ApplyComboSlow();
        }
    }

    private void ApplySingleSlow()
    {
        // 如果 combo 已激活，检查是否还有异丙嗪
        if (_comboActivated)
        {
            var itemManager = _target?.GetComponent<ItemManager>();
            if (itemManager != null && itemManager.HasItem("promethazine"))
            {
                ApplyComboSlow();
                return;
            }
            // 丢了异丙嗪 → 降级为单件
            _comboActivated = false;
        }

        EnemyMovement.GlobalSpeedMultiplier = _enemySlowFactor;
        Projectile.GlobalSpeedMultiplier = _bulletSlowFactor;
    }

    private void ApplyComboSlow()
    {
        EnemyMovement.GlobalSpeedMultiplier = _comboEnemySlowFactor;
        Projectile.GlobalSpeedMultiplier = _comboBulletSlowFactor;
    }
}
