using UnityEngine;

/// <summary>
/// 侵蚀投射物命中逻辑 — 仅处理命中敌人后的夺取弹窗。
/// 飞行由 Projectile 组件（ProjectileSkillEffect 自动添加）负责。
/// 挂载在 ErodeProjectile 预制体上，与 Projectile 组件共存。
/// </summary>
public class ErodeProjectile : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 只命中敌人
        if (!other.CompareTag("Enemy")) return;

        IEnemy enemy = other.GetComponent<IEnemy>();
        if (enemy == null) return;

        // 检查敌人是否有技能
        var enemySkills = enemy.SkillInstances;
        if (enemySkills == null || enemySkills.Count == 0) return;

        // 获取玩家技能管理器
        PlayerSkillManager player = FindPlayerSkillManager();
        if (player == null) return;

        // 暂停 + 弹窗
        Time.timeScale = 0f;

        SkillStealPopupManager popup = SkillStealPopupManager.Instance;
        if (popup != null)
        {
            popup.ShowPopup(enemySkills, player, () => Time.timeScale = 1f);
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    private static PlayerSkillManager FindPlayerSkillManager()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<PlayerSkillManager>() : null;
    }
}
