using System.Linq;
using UnityEngine;

/// <summary>
/// 骨髓应急崩解 buff 效果 — 施加瞬间：
/// 1) 施法者永久扣除 1/2 血上限；
/// 2) 玩家施放：立即清除地图内所有非 Boss 敌人，Boss 减少 1/4 血量；
///    敌人施放：扣除玩家及其随从（同化敌人）1/4 血量。
/// buff 是短时增益（效果一次性完成）。
/// 泛用"献祭/爆发"类 buff 效果：可作为牺牲型技能的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "BoneMarrowBuff", menuName = "Game/Buff Effect/Bone Marrow Emergency")]
public class BoneMarrowBuff : BuffEffectBase
{
    [Header("崩解配置")]
    [Tooltip("自身血上限扣除比例（0.5 = 扣一半）")]
    public float maxHealthCostRatio = 0.5f;

    [Tooltip("Boss 血量扣除比例（0.25 = 扣 1/4）")]
    public float bossDamageRatio = 0.25f;

    [Tooltip("敌人施放时对玩家/随从的伤害比例（0.25 = 扣 1/4）")]
    public float playerDamageRatio = 0.25f;

    [Tooltip("清场光效材质（null=无视觉）")]
    public Material burstMaterial;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        // 1. 施法者永久扣除血上限
        ApplyHealthCost(target);

        // 2. 按施法者阵营执行效果（玩家清敌 / 敌人扣玩家及随从血）
        bool isPlayer = target.GetComponent<PlayerStats>() != null;
        if (isPlayer)
            ClearEnemies(target);
        else
            DamagePlayerAndFollowers();

        // 3. 清场光效
        CreateBurstVisual(target);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        // 效果一次性完成，无需处理
    }

    /// <summary>施法者永久扣除血上限（当前血量保持不变，仅受限于新上限截断）</summary>
    private void ApplyHealthCost(GameObject target)
    {
        var stats = target.GetComponent<PlayerStats>();
        if (stats != null)
        {
            float newMax = stats.MaxHealth * (1f - maxHealthCostRatio);
            newMax = Mathf.Max(newMax, 1f);
            // 只扣血上限：当前血量不变（若超过新上限会被 SetStatValue 自动截断）
            stats.SetStatValue("MaxHealth", newMax);

            Debug.Log($"[BoneMarrow] 自身血上限扣除 {maxHealthCostRatio * 100f:F0}% → {newMax}（当前血保持）");
            return;
        }

        // 非玩家（敌人）施放时：EnemyHealth 处理
        var health = target.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.MaxHealth = Mathf.Max(health.MaxHealth * (1f - maxHealthCostRatio), 1f);
        }
    }

    /// <summary>敌人施放：扣除玩家及其随从（同化敌人）1/4 血量</summary>
    private void DamagePlayerAndFollowers()
    {
        // 玩家（带 PlayerStats 组件；随从 tag 也是 Player，不能按 tag 找）
        var playerStats = Object.FindObjectsOfType<PlayerStats>().FirstOrDefault();
        if (playerStats != null)
        {
            playerStats.TakeDamage(playerStats.MaxHealth * playerDamageRatio);
            Debug.Log($"[BoneMarrow] 扣除玩家 {playerDamageRatio * 100f:F0}% 血");
        }

        // 随从（同化敌人，IsAssimilated）
        var enemies = Object.FindObjectsOfType<EnemyCore>();
        int followerCount = 0;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || !enemy.IsAssimilated) continue;
            var health = enemy.Health;
            if (health != null)
            {
                health.TakeDamage(health.MaxHealth * playerDamageRatio);
                followerCount++;
            }
        }
        if (followerCount > 0)
            Debug.Log($"[BoneMarrow] 扣除 {followerCount} 个随从 {playerDamageRatio * 100f:F0}% 血");
    }

    /// <summary>清除所有非 Boss 敌人，Boss 扣 1/4 血（排除施法者自身）</summary>
    private void ClearEnemies(GameObject caster)
    {
        var enemies = Object.FindObjectsOfType<EnemyCore>();
        int cleared = 0;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || enemy.IsAssimilated) continue;

            // 排除施法者自身（敌人施放时不自杀）
            if (caster != null && enemy.gameObject == caster) continue;

            // Boss：扣 1/4 血（走正常伤害流程，可能触发阶段切换）
            if (enemy.GetComponent<BossCore>() != null)
            {
                var bossHealth = enemy.Health;
                if (bossHealth != null)
                {
                    float bossDamage = bossHealth.MaxHealth * bossDamageRatio;
                    bossHealth.TakeDamage(bossDamage);
                    Debug.Log($"[BoneMarrow] Boss 扣除 {bossDamage:F0} 血（{bossDamageRatio * 100f:F0}%）");
                }
                continue;
            }

            // 普通敌人：直接击杀（触发正常死亡流程，房间清空判定生效）
            var health = enemy.Health;
            if (health != null)
            {
                health.TakeDamage(health.CurrentHealth + 9999f); // 确保致死
                cleared++;
            }
        }
        Debug.Log($"[BoneMarrow] 清除 {cleared} 个普通敌人");
    }

    /// <summary>创建一次性清场光效（大范围扩散，0.7s 后销毁）</summary>
    private void CreateBurstVisual(GameObject target)
    {
        var go = new GameObject("BoneMarrowBurst");
        go.transform.position = target.transform.position;

        var sr = go.AddComponent<SpriteRenderer>();
        if (burstMaterial != null)
            sr.material = new Material(burstMaterial);
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 95;

        var anim = go.AddComponent<BurstAnimator>();
        anim.Run();
    }

    /// <summary>大范围扩散动画组件</summary>
    private class BurstAnimator : MonoBehaviour
    {
        private float _timer;
        private const float Duration = 0.7f;
        private SpriteRenderer _sr;

        public void Run() { _sr = GetComponent<SpriteRenderer>(); }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / Duration);

            float scale = Mathf.Lerp(1f, 5f, t);
            transform.localScale = new Vector3(scale, scale, 1f);

            if (_sr != null)
            {
                var c = _sr.color;
                c.a = 1f - t;
                _sr.color = c;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>生成纯白 1x1 sprite（PPU=1）</summary>
    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
