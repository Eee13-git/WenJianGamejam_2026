using System;
using UnityEngine;

/// <summary>
/// 伤害吸收护盾组件 — 挂在角色身上，提供一定量的伤害吸收 + 免伤比例。
/// 在 EnemyStats.TakeDamage / PlayerStats.TakeDamage 中检查此组件：
/// 1) 伤害先按免伤比例降低；
/// 2) 剩余伤害从护盾 HP 中扣除；
/// 3) 护盾 HP 耗尽 → 触发 OnShieldBroken 事件 + 销毁组件。
/// 泛用组件：可被任何需要"吸收伤害护盾"的机制复用。
/// </summary>
public class AbsorbShield : MonoBehaviour
{
    /// <summary>当前护盾 HP</summary>
    public float ShieldHP { get; private set; }

    /// <summary>最大护盾 HP</summary>
    public float MaxShieldHP { get; private set; }

    /// <summary>免伤比例（0=无减免，0.5=减半）</summary>
    public float DamageReduction { get; private set; }

    /// <summary>护盾剩余时间</summary>
    public float RemainingTime { get; private set; }

    /// <summary>是否激活中</summary>
    public bool IsActive => ShieldHP > 0f && RemainingTime > 0f;

    /// <summary>护盾破碎事件</summary>
    public event Action OnShieldBroken;

    private SpriteRenderer _visualRenderer;
    private Material _matInstance;

    /// <summary>初始化护盾</summary>
    public void Initialize(float shieldHP, float damageReduction, float duration, Material shieldMaterial)
    {
        MaxShieldHP = shieldHP;
        ShieldHP = shieldHP;
        DamageReduction = Mathf.Clamp(damageReduction, 0f, 0.95f);
        RemainingTime = duration;

        CreateVisual(shieldMaterial);
    }

    /// <summary>
    /// 尝试吸收伤害。修改 damage 参数为穿透护盾后的实际伤害。
    /// 返回 true = 护盾仍在（部分或全部伤害被吸收）。
    /// 返回 false = 护盾不存在（不修改 damage）。
    /// </summary>
    public bool TryAbsorb(ref float damage)
    {
        if (!IsActive) return false;

        // 1. 免伤比例降低
        damage *= (1f - DamageReduction);

        // 2. 护盾吸收
        if (damage <= ShieldHP)
        {
            // 护盾完全吸收
            ShieldHP -= damage;
            damage = 0f;

            // 命中闪白
            if (_matInstance != null)
                _matInstance.SetFloat("_HitFlash", 1f);

            return true;
        }

        // 护盾不足，穿透
        damage -= ShieldHP;
        ShieldHP = 0f;

        // 护盾破碎
        BreakShield();
        return false;
    }

    private void Update()
    {
        if (!IsActive) return;

        RemainingTime -= Time.deltaTime;

        // 闪白衰减
        if (_matInstance != null)
        {
            float flash = _matInstance.GetFloat("_HitFlash");
            if (flash > 0f)
                _matInstance.SetFloat("_HitFlash", Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f));
        }

        // 视觉跟随
        if (_visualRenderer != null)
            _visualRenderer.transform.position = transform.position;

        // 时间到 → 护盾自然消失（不触发破碎）
        if (RemainingTime <= 0f)
        {
            ShieldHP = 0f;
            DestroyVisual();
            Destroy(this);
        }
    }

    /// <summary>护盾破碎（被击碎）— 播放破碎动画后销毁</summary>
    private void BreakShield()
    {
        OnShieldBroken?.Invoke();

        // 破碎视觉：快速放大 + 闪白淡出
        if (_visualRenderer != null && _matInstance != null)
        {
            _matInstance.SetFloat("_Break", 1f);
            _matInstance.SetFloat("_HitFlash", 1f);
        }

        // 延迟销毁视觉（让破碎动画播完）
        if (_visualRenderer != null)
        {
            // 简单做法：直接在 0.15s 后销毁
            Destroy(_visualRenderer.gameObject, 0.15f);
        }
        _visualRenderer = null;
        _matInstance = null;

        Destroy(this);
    }

    private void CreateVisual(Material shieldMaterial)
    {
        if (shieldMaterial == null)
        {
            Debug.LogWarning("[AbsorbShield] shieldMaterial 为 null，无视觉");
            return;
        }

        var go = new GameObject("AbsorbShieldVisual");
        go.transform.SetParent(transform);
        go.transform.position = transform.position;

        _visualRenderer = go.AddComponent<SpriteRenderer>();
        _matInstance = new Material(shieldMaterial);
        _visualRenderer.material = _matInstance;
        _visualRenderer.sprite = CreateWhiteSprite();
        _visualRenderer.sortingOrder = 80;
        // PPU=1 的白 sprite，scale 直接等于世界尺寸
        _visualRenderer.transform.localScale = new Vector3(2.2f, 2.2f, 1f);

        Debug.Log($"[AbsorbShield] 视觉已创建: pos={go.transform.position}, scale={_visualRenderer.transform.localScale}, sprite={_visualRenderer.sprite?.name}, mat={_matInstance?.name}, sortingOrder={_visualRenderer.sortingOrder}");
    }

    private void DestroyVisual()
    {
        if (_visualRenderer != null && _visualRenderer.gameObject != null)
            Destroy(_visualRenderer.gameObject);
        _visualRenderer = null;
    }

    private void OnDestroy()
    {
        DestroyVisual();
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
