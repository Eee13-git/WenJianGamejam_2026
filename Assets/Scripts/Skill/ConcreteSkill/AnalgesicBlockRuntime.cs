using UnityEngine;

/// <summary>
/// 镇痛阻滞运行时组件 — 挂在角色身上，处理"伤害延迟"。
/// buff 激活期间：受到伤害仅 50% 立即扣除，另外 50% 记入延迟伤害池。
/// buff 结束后：延迟伤害池转为"痛觉残留"debuff 缓慢扣除。
/// 同时提供硬直免疫标志（IgnoreStun）。
/// </summary>
public class AnalgesicBlockRuntime : MonoBehaviour
{
    /// <summary>立即扣除比例（0.5 = 一半立即扣，一半延迟）</summary>
    public float ImmediateFactor { get; set; } = 0.5f;

    /// <summary>是否激活（buff 持续期间）</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>累计的延迟伤害池</summary>
    public float PendingDamage { get; private set; }

    // ── 视觉 ──
    private GameObject _visualGO;
    private Material _matInstance;

    /// <summary>激活镇痛（创建视觉）</summary>
    public void Activate(Material fieldMaterial)
    {
        CreateVisual(fieldMaterial);
    }

    /// <summary>把伤害拆分为立即 + 延迟，返回立即扣除部分</summary>
    public float SplitDamage(float damage)
    {
        if (!IsActive || damage <= 0f) return damage;

        float immediate = damage * ImmediateFactor;
        float delayed = damage - immediate;
        if (delayed > 0f)
            PendingDamage += delayed;
        return immediate;
    }

    /// <summary>取走整个延迟伤害池（并清零），供"痛觉残留"debuff 使用</summary>
    public float TakePendingDamage()
    {
        float total = PendingDamage;
        PendingDamage = 0f;
        return total;
    }

    /// <summary>直接扣减池中伤害（痛觉残留 DOT 每 tick 调用）</summary>
    public void ConsumePending(float amount)
    {
        PendingDamage = Mathf.Max(0f, PendingDamage - amount);
    }

    /// <summary>清除延迟池（无残留伤害时调用）</summary>
    public void ClearPending()
    {
        PendingDamage = 0f;
    }

    /// <summary>是否还有未结算的延迟伤害</summary>
    public bool HasPending => PendingDamage > 0.01f;

    private void CreateVisual(Material fieldMaterial)
    {
        _visualGO = new GameObject("AnalgesicField");
        _visualGO.transform.SetParent(transform);
        _visualGO.transform.position = transform.position;

        var sr = _visualGO.AddComponent<SpriteRenderer>();
        if (fieldMaterial != null)
        {
            _matInstance = new Material(fieldMaterial);
            sr.material = _matInstance;
        }
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 80;
    }

    private void Update()
    {
        // 视觉跟随
        if (_visualGO != null)
            _visualGO.transform.position = transform.position;
    }

    private void OnDestroy()
    {
        if (_visualGO != null)
            Destroy(_visualGO);
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
