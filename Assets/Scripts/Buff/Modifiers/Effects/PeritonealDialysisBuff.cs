using UnityEngine;

/// <summary>
/// 腹膜透析 buff 效果 — 施加瞬间：
/// 1) 清除自身所有可净化的 Debuff（藏品/道具来源的 Indestructible debuff 不可清除）；
/// 2) 根据清除的 debuff 数量回复一定血量（每清除一个恢复 healPerDebuff 点）。
/// buff 本身是短时增益（显示净化状态），效果在 OnApply 一次性执行。
/// 泛用"净化"类 buff 效果：可作为清除 debuff + 回血技能的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "PeritonealDialysisBuff", menuName = "Game/Buff Effect/Peritoneal Dialysis")]
public class PeritonealDialysisBuff : BuffEffectBase
{
    [Header("净化配置")]
    [Tooltip("每清除一个 debuff 回复的血量")]
    public float healPerDebuff = 20f;

    [Tooltip("净化光效材质（null=无视觉）")]
    public Material purifyMaterial;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var buffManager = target.GetComponent<BuffManager>();
        if (buffManager == null) return;

        // 1. 清除所有可净化的 debuff（返回数量）
        int clearedCount = buffManager.RemoveAllDebuffs();

        // 2. 按清除数量回血
        if (clearedCount > 0)
        {
            var healable = target.GetComponent<IHealable>();
            healable?.Heal(clearedCount * healPerDebuff);
        }

        Debug.Log($"[PeritonealDialysis] 清除 {clearedCount} 个 debuff，回复 {clearedCount * healPerDebuff} 血量");

        // 3. 净化光效
        CreatePurifyVisual(target);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        // 效果已一次性完成，无需额外处理
    }

    /// <summary>创建一次性净化光效（扩散光环，0.6s 后销毁）</summary>
    private void CreatePurifyVisual(GameObject target)
    {
        var go = new GameObject("PeritonealPurifyVFX");
        go.transform.position = target.transform.position;

        var sr = go.AddComponent<SpriteRenderer>();
        if (purifyMaterial != null)
            sr.material = new Material(purifyMaterial);
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 95;

        // 扩散放大 + 淡出后销毁
        var anim = go.AddComponent<PurifyAnimator>();
        anim.Run();
    }

    /// <summary>扩散动画组件</summary>
    private class PurifyAnimator : MonoBehaviour
    {
        private float _timer;
        private const float Duration = 0.6f;
        private SpriteRenderer _sr;

        public void Run()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / Duration);

            // 快速放大扩散
            float scale = Mathf.Lerp(1f, 3.5f, t);
            transform.localScale = new Vector3(scale, scale, 1f);

            // 淡出
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = 1f - t;
                _sr.color = c;
            }

            if (t >= 1f)
                Destroy(gameObject);
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
