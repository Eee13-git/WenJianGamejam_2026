using UnityEngine;

/// <summary>
/// 侵蚀投射物命中逻辑 — 命中敌人后弹出二选一弹窗（吞噬 / 同化）。
/// 飞行由 Projectile 组件（ProjectileSkillEffect 自动添加）负责。
/// 挂载在 ErodeProjectile 预制体上，与 Projectile 组件共存。
/// </summary>
public class ErodeProjectile : MonoBehaviour
{
    [Header("特效")]
    [SerializeField] private GameObject hitEffectPrefab;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 只命中敌人
        if (!other.CompareTag("Enemy")) return;

        IEnemy enemy = other.GetComponent<IEnemy>();
        if (enemy == null) return;

        // 获取玩家技能管理器
        PlayerSkillManager player = FindPlayerSkillManager();
        if (player == null) return;

        var enemySkills = enemy.SkillInstances;
        bool hasSkills = enemySkills != null && enemySkills.Count > 0;

        // 暂停 + 弹窗
        Time.timeScale = 0f;

        EnemyCore core = enemy as EnemyCore;
        ErodeChoicePopupManager popup = ErodeChoicePopupManager.Instance;
        if (popup != null && core != null)
        {
            popup.ShowPopup(core, hasSkills, enemySkills, player, () => Time.timeScale = 1f);
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    private void OnDestroy()
    {
        // 生成命中爆发特效
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // 将拖尾粒子子物体分离，让它自然消散
        var trail = transform.Find("TrailParticles");
        if (trail != null)
        {
            trail.SetParent(null);
            var ps = trail.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private static PlayerSkillManager FindPlayerSkillManager()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<PlayerSkillManager>() : null;
    }
}
