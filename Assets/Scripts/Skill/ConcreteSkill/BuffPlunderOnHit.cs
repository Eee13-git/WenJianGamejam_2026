using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 因子掠夺命中组件 — 挂在投射物预制体上。
/// 命中目标后：从其身上偷取一个增益 buff（排除 Indestructible 藏品 buff），
/// 转移到施法者身上并持续 duration 秒。
/// 泛用"偷取增益"投射物组件，可复用于任何掠夺/窃取类技能。
/// </summary>
public class BuffPlunderOnHit : MonoBehaviour
{
    [Header("掠夺配置")]
    [Tooltip("偷来的 buff 持续时间（秒）")]
    [SerializeField] private float _duration = 10f;

    private Projectile _proj;

    /// <summary>设置偷取 buff 时长（由技能效果调用覆盖 Inspector 值）</summary>
    public void SetDuration(float duration)
    {
        _duration = Mathf.Max(duration, 0f);
    }

    private void Awake()
    {
        _proj = GetComponent<Projectile>();

        // 修复：保存预制体时运行时创建的 Texture2D 引用丢失 → sprite 为 null 不渲染。
        // 若 sprite 缺失则动态生成柔边圆（投射物视觉）。
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
            sr.sprite = CreateGlowSprite();
    }

    /// <summary>生成柔边圆 sprite（投射物视觉，材质 tint 控制颜色）</summary>
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

    /// <summary>
    /// 由 Projectile.OnTriggerEnter2D 调用（命中目标后触发偷取）。
    /// 若已初始化则自动触发。
    /// </summary>
    public void TryPlunder(GameObject target)
    {
        if (_proj == null || !_proj.enabled) return;

        var casterGO = _proj.Caster;
        if (casterGO == null) return;

        // 从目标身上偷取一个增益 buff
        var targetBuffMgr = target.GetComponent<BuffManager>();
        if (targetBuffMgr == null) return;

        var stolen = StealBuff(targetBuffMgr, casterGO);
        Debug.Log(stolen != null
            ? $"[FactorPlunder] 偷取增益 '{stolen.Data.buffId}' 给施法者"
            : "[FactorPlunder] 目标无可偷取的增益 buff");
    }

    /// <summary>从目标 BuffManager 偷取一个增益 buff 到施法者（返回偷到的 buff）</summary>
    private BuffInstance StealBuff(BuffManager targetBuffMgr, GameObject casterGO)
    {
        // 找一个增益 buff（排除 Indestructible 藏品 buff）
        BuffInstance target = null;
        foreach (var buff in targetBuffMgr.ActiveBuffs)
        {
            if (buff == null || !buff.IsActive) continue;
            if (buff.Data.buffType != BuffType.Buff) continue;
            if (buff.Indestructible) continue;   // 藏品 buff 不可偷
            target = buff;
            break;
        }

        if (target == null) return null;

        // 从目标移除
        BuffData data = target.Data;
        targetBuffMgr.RemoveBuff(target);

        // 转移到施法者（覆盖为指定时长）
        var casterBuffMgr = casterGO.GetComponent<BuffManager>();
        if (casterBuffMgr == null) return null;

        var stolen = casterBuffMgr.ApplyBuff(data, casterGO);
        if (stolen != null && _duration > 0f)
            stolen.SetRemainingDuration(_duration);
        return stolen;
    }
}
