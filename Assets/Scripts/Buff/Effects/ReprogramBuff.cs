using UnityEngine;

/// <summary>
/// 细胞部分重编程 buff 效果 — 施加瞬间：
/// 刷新施法者剩余冷却时间最长的技能（立即冷却完毕）。
/// buff 是短时增益（效果一次性完成）。
/// 泛用"刷新冷却"类 buff 效果：可作为技能刷新技能的效果策略复用。
/// </summary>
[CreateAssetMenu(fileName = "ReprogramBuff", menuName = "Game/Buff Effect/Cell Reprogramming")]
public class ReprogramBuff : BuffEffectBase
{
    [Tooltip("重编程光效材质（null=无视觉）")]
    public Material reprogramMaterial;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        // 刷新冷却最长的技能（玩家/敌人通用）
        // 排除自身：施放中的本技能不应刷新自己的冷却
        string selfId = buff.Data.buffId;

        SkillInstance reset = null;

        var psm = target.GetComponent<PlayerSkillManager>();
        if (psm != null)
        {
            reset = psm.ResetLongestCooldownSkill(selfId);
        }
        else
        {
            var esm = target.GetComponent<EnemySkillManager>();
            if (esm != null)
                reset = esm.ResetLongestCooldownSkill(selfId);
        }

        Debug.Log(reset != null
            ? $"[Reprogram] 已刷新技能 '{reset.Data.skillId}' 冷却"
            : "[Reprogram] 没有冷却中的技能可刷新");

        // 重编程光效
        CreateVisual(target);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        // 效果一次性完成，无需处理
    }

    /// <summary>创建一次性重编程光效（扩散光环，0.5s 后销毁）</summary>
    private void CreateVisual(GameObject target)
    {
        var go = new GameObject("ReprogramVFX");
        go.transform.position = target.transform.position;

        var sr = go.AddComponent<SpriteRenderer>();
        if (reprogramMaterial != null)
            sr.material = new Material(reprogramMaterial);
        sr.sprite = CreateGlowSprite();
        sr.sortingOrder = 90;

        var anim = go.AddComponent<BurstAnimator>();
        anim.Run();
    }

    /// <summary>扩散动画组件</summary>
    private class BurstAnimator : MonoBehaviour
    {
        private float _timer;
        private const float Duration = 0.5f;
        private SpriteRenderer _sr;

        public void Run() { _sr = GetComponent<SpriteRenderer>(); }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / Duration);

            float scale = Mathf.Lerp(1f, 3f, t);
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

    /// <summary>生成柔边圆 sprite</summary>
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
