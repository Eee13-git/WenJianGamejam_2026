using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 退行性病变 buff 效果 — 使房间内所有敌人的伤害降低。
/// 由 ApplyBuffSkillEffect 施加到施法者身上（buff 持续 5s）。
/// OnApply — 遍历场景所有敌人（排除死亡/已同化），降低其近战/远程伤害并附加视觉标记；
/// OnRemove — 恢复所有敌人伤害并移除标记。
/// 泛用"虚弱/削弱"类 buff 效果：可作为范围降攻技能的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "RegressiveDiseaseBuff", menuName = "Game/Buff Effect/Regressive Disease")]
public class RegressiveDiseaseBuff : BuffEffectBase
{
    [Header("削弱配置")]
    [Tooltip("伤害倍率（0.5 = 敌人伤害减半）")]
    public float damageFactor = 0.5f;

    [Tooltip("削弱标记材质（null=无视觉）")]
    public Material markMaterial;

    /// <summary>记录被削弱敌人的原始伤害值（用于恢复）</summary>
    private static readonly Dictionary<BuffInstance, List<EnemyDamageRecord>> _records
        = new Dictionary<BuffInstance, List<EnemyDamageRecord>>();

    private class EnemyDamageRecord
    {
        public EnemyCore enemy;
        public MeleeAttack melee;
        public RangedAttack ranged;
        public float meleeDamage;
        public float rangedDamage;
        public GameObject mark;   // 视觉标记
    }

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var records = new List<EnemyDamageRecord>();

        // 遍历场景所有敌人
        var enemies = Object.FindObjectsOfType<EnemyCore>();
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || enemy.IsAssimilated) continue;

            var record = new EnemyDamageRecord();
            record.enemy = enemy;
            record.melee = enemy.GetComponent<MeleeAttack>();
            record.ranged = enemy.GetComponent<RangedAttack>();

            // 记录原始值并降低伤害
            bool modified = false;
            if (record.melee != null)
            {
                record.meleeDamage = record.melee.damage;
                record.melee.damage *= damageFactor;
                modified = true;
            }
            if (record.ranged != null)
            {
                record.rangedDamage = record.ranged.damage;
                record.ranged.damage *= damageFactor;
                modified = true;
            }

            if (modified)
            {
                record.mark = CreateMark(enemy);
                records.Add(record);
            }
        }

        _records[buff] = records;
        Debug.Log($"[RegressiveDisease] 削弱 {records.Count} 个敌人伤害 ({damageFactor:F0}%)");
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (!_records.TryGetValue(buff, out var records)) return;

        // 恢复所有敌人伤害并移除标记
        foreach (var record in records)
        {
            if (record.enemy == null) continue;
            if (record.melee != null) record.melee.damage = record.meleeDamage;
            if (record.ranged != null) record.ranged.damage = record.rangedDamage;
            if (record.mark != null) Object.Destroy(record.mark);
        }

        _records.Remove(buff);
        Debug.Log("[RegressiveDisease] 敌人伤害已恢复");
    }

    /// <summary>给敌人创建紫色削弱标记（跟随敌人）</summary>
    private GameObject CreateMark(EnemyCore enemy)
    {
        var go = new GameObject("RegressiveMark");
        go.transform.SetParent(enemy.transform);
        go.transform.localPosition = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        if (markMaterial != null)
            sr.material = new Material(markMaterial);
        sr.sprite = CreateGlowSprite();
        sr.sortingOrder = 75;
        go.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        return go;
    }

    /// <summary>生成径向柔边圆 sprite（紫色 tint 由材质控制）</summary>
    private static Sprite CreateGlowSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.SmoothStep(0f, 1f, a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
