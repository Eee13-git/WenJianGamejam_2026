using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 泛用电击新星技能效果 — 以施法者为中心对范围内所有敌方单位造成电击伤害，
/// 并同时从施法者向每个命中目标放射一道锯齿闪电弧（复用 ChainLightningArcRuntime）。
/// 不使用投射物，纯瞬间范围电击。所有数值与视觉均可通过资产配置，可被多个电击类技能复用。
/// 右键 -> Create -> Game -> Skill Effect -> Electric Nova
/// </summary>
[CreateAssetMenu(fileName = "ElectricNovaEffect", menuName = "Game/Skill Effect/Electric Nova")]
public class ElectricNovaSkillEffect : SkillEffectBase
{
    [Header("范围配置")]
    [Tooltip("电击伤害半径")]
    public float radius = 3f;

    [Header("闪电弧视觉")]
    [Tooltip("闪电弧材质（加法混合发光）")]
    public Material arcMaterial;
    [Tooltip("闪电弧锯齿段数")]
    public int arcSegments = 8;
    [Tooltip("闪电弧抖动幅度")]
    public float arcJitter = 0.35f;
    [Tooltip("闪电弧宽度")]
    public float arcWidth = 0.12f;
    [Tooltip("闪电弧持续时间")]
    public float arcDuration = 0.22f;
    [Tooltip("闪电弧颜色（核心）")]
    public Color arcColor = new Color(0.6f, 0.95f, 1f, 1f);
    [Tooltip("闪电弧顶点刷新间隔（帧）")]
    public int arcRefreshInterval = 3;

    [Header("命中特效")]
    [Tooltip("命中辉光贴图（可选）")]
    public Sprite hitGlowSprite;
    [Tooltip("命中辉光颜色")]
    public Color hitGlowColor = new Color(0.7f, 1f, 1f, 1f);
    [Tooltip("命中辉光初始大小")]
    public float hitGlowSize = 1.2f;
    [Tooltip("命中辉光持续时间")]
    public float hitGlowDuration = 0.25f;

    public override void Execute(ISkillCaster caster, Vector2 direction,
                                  float damageMultiplier, Projectile.OwnerType ownerType)
    {
        if (caster == null || caster.CasterTransform == null) return;

        Vector3 center = caster.CasterTransform.position;
        float damage = caster.GetAttackStrength() * damageMultiplier;
        string targetTag = ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);

        bool hitAny = false;
        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;

            // 跳过死亡敌人
            if (hit.TryGetComponent<EnemyCore>(out var ec) && ec.IsDead) continue;

            // 从施法者向目标放射闪电弧
            SpawnArc(center, hit.transform.position);
            SpawnHitGlow(hit.transform.position);

            if (hit.TryGetComponent<IDamageable>(out var d))
                d.TakeDamage(damage);

            hitAny = true;
        }

        // 无目标时向施法方向空放一道闪电（视觉反馈）
        if (!hitAny)
            SpawnArc(center, center + (Vector3)(direction.normalized * radius));
    }

    /// <summary>生成锯齿状闪电弧（复用泛用运行时组件）</summary>
    private void SpawnArc(Vector3 from, Vector3 to)
    {
        if (arcMaterial == null) return;
        float distance = Vector3.Distance(from, to);
        if (distance < 0.01f) return;

        var go = new GameObject("ElectricNovaArc");
        go.transform.position = Vector3.zero;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = arcSegments + 1;
        lr.startWidth = arcWidth;
        lr.endWidth = arcWidth * 0.6f;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.sortingOrder = 25;
        lr.material = arcMaterial;

        var arc = go.AddComponent<ChainLightningArcRuntime>();
        arc.Init(lr, from, to, arcSegments, arcJitter, arcDuration, arcColor, arcRefreshInterval);

        Destroy(go, arcDuration);
    }

    /// <summary>命中位置生成辉光闪光（复用泛用运行时组件）</summary>
    private void SpawnHitGlow(Vector3 pos)
    {
        var go = new GameObject("ElectricNovaHitGlow");
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 26;
        if (hitGlowSprite != null)
        {
            sr.sprite = hitGlowSprite;
            sr.color = hitGlowColor;
        }

        var glow = go.AddComponent<ChainLightningHitGlowRuntime>();
        glow.Init(sr, hitGlowSize, hitGlowDuration);

        Destroy(go, hitGlowDuration);
    }
}
