using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 中性粒细胞净化光环 buff 效果 — 被动永久光环：
/// 1) 每 selfHealTickInterval 秒恢复自身 selfHealPerTick 点血量；
/// 2) 每 auraTickInterval 秒对范围内友方单位清除所有可净化的 Debuff
///    （藏品/道具来源的 Indestructible debuff 不可清除）；
/// 3) 根据每个友方被清除的 debuff 数量，为其回复 healPerDebuff * 数量 点血量。
/// 泛用"净化光环"类 buff 效果：可作为持续回血 + 周期性净化友方的支援型技能效果复用。
/// </summary>
[CreateAssetMenu(fileName = "NeutrophilAuraBuff", menuName = "Game/Buff Effect/Neutrophil Purify Aura")]
public class NeutrophilAuraBuff : BuffEffectBase
{
    [Header("自身回血")]
    [Tooltip("每次自身回血的量")]
    public float selfHealPerTick = 2f;
    [Tooltip("自身回血间隔（秒）")]
    public float selfHealTickInterval = 1f;

    [Header("净化光环")]
    [Tooltip("光环半径（范围内友方单位）")]
    public float auraRadius = 6f;
    [Tooltip("光环净化间隔（秒）")]
    public float auraTickInterval = 3f;
    [Tooltip("每清除一个 debuff 为该单位回复的血量")]
    public float healPerDebuff = 10f;

    [Header("视觉（可选）")]
    [Tooltip("净化瞬间光效材质（null=无净化光效）")]
    public Material purifyMaterial;

    // 两个独立计时器（key 为 BuffInstance）
    private static readonly Dictionary<BuffInstance, float> _selfHealTimers = new Dictionary<BuffInstance, float>();
    private static readonly Dictionary<BuffInstance, float> _auraTimers = new Dictionary<BuffInstance, float>();

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _selfHealTimers[buff] = 0f;
        _auraTimers[buff] = 0f;
    }

    public override void OnTick(GameObject target, BuffInstance buff, float deltaTime)
    {
        TickSelfHeal(target, buff, deltaTime);
        TickAura(target, buff, deltaTime);
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        _selfHealTimers.Remove(buff);
        _auraTimers.Remove(buff);
    }

    // ---------- 自身持续回血 ----------
    private void TickSelfHeal(GameObject target, BuffInstance buff, float deltaTime)
    {
        if (!_selfHealTimers.TryGetValue(buff, out float timer))
        {
            timer = 0f;
            _selfHealTimers[buff] = timer;
        }

        timer += deltaTime;
        if (timer < selfHealTickInterval)
        {
            _selfHealTimers[buff] = timer;
            return;
        }

        int tickCount = Mathf.FloorToInt(timer / selfHealTickInterval);
        _selfHealTimers[buff] = timer - selfHealTickInterval * tickCount;

        var healable = target.GetComponent<IHealable>();
        healable?.Heal(selfHealPerTick * tickCount);
    }

    // ---------- 周期性净化友方 ----------
    private void TickAura(GameObject target, BuffInstance buff, float deltaTime)
    {
        if (!_auraTimers.TryGetValue(buff, out float timer))
        {
            timer = 0f;
            _auraTimers[buff] = timer;
        }

        timer += deltaTime;
        if (timer < auraTickInterval)
        {
            _auraTimers[buff] = timer;
            return;
        }

        int tickCount = Mathf.FloorToInt(timer / auraTickInterval);
        _auraTimers[buff] = timer - auraTickInterval * tickCount;

        // 只执行一次净化（若帧间隔远超 tickInterval，也仅净化一次，避免集中爆发）
        PurifyAllies(target);
    }

    /// <summary>对范围内友方单位清除 debuff 并按数量回血（每次净化周期均播放净化光效）</summary>
    private void PurifyAllies(GameObject caster)
    {
        var allies = GetAllies(caster);
        int totalCleared = 0;

        foreach (var ally in allies)
        {
            if (ally == null) continue;
            if (Vector2.Distance(ally.transform.position, caster.transform.position) > auraRadius) continue;

            var buffMgr = ally.GetComponent<BuffManager>();

            int clearedCount = 0;
            if (buffMgr != null)
                clearedCount = buffMgr.RemoveAllDebuffs();

            if (clearedCount > 0)
            {
                totalCleared += clearedCount;

                // 按清除数量回血
                var healable = ally.GetComponent<IHealable>();
                healable?.Heal(clearedCount * healPerDebuff);
            }

            // 每个友方闪一个小环
            CreatePurifyVisual(ally, 0.6f, 1.8f);
        }

        // 中性粒细胞自身位置闪一个大环（覆盖净化范围）
        CreatePurifyVisual(caster, 1f, 4.5f);

        if (totalCleared > 0)
            Debug.Log($"[NeutrophilAura] 净化 {totalCleared} 个 debuff（{allies.Count} 个友方单位）");
    }

    /// <summary>获取施法者的友方单位（按阵营区分）</summary>
    private List<GameObject> GetAllies(GameObject caster)
    {
        var list = new List<GameObject>();
        bool isPlayerSide = caster.CompareTag("Player");

        if (isPlayerSide)
        {
            // 玩家方：玩家本体 + 随从（同化敌人）
            var players = Object.FindObjectsOfType<PlayerStats>();
            foreach (var p in players)
                if (p != null && !p.IsDead) list.Add(p.gameObject);

            var followers = Object.FindObjectsOfType<EnemyCore>();
            foreach (var f in followers)
                if (f != null && f.IsAssimilated && !f.IsDead) list.Add(f.gameObject);
        }
        else
        {
            // 敌方：其他存活且未同化的敌人（排除自身）
            var enemies = Object.FindObjectsOfType<EnemyCore>();
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead || e.IsAssimilated) continue;
                if (e.gameObject == caster) continue;
                list.Add(e.gameObject);
            }
        }

        return list;
    }

    /// <summary>创建一次性净化光效（扩散光环，从 startScale 扩散到 endScale 后销毁）</summary>
    private void CreatePurifyVisual(GameObject target, float startScale, float endScale)
    {
        var go = new GameObject("NeutrophilPurifyVFX");
        go.transform.position = target.transform.position;

        var sr = go.AddComponent<SpriteRenderer>();
        if (purifyMaterial != null)
            sr.material = new Material(purifyMaterial);
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 95;

        var anim = go.AddComponent<PurifyAnimator>();
        anim.Run(startScale, endScale);
    }

    /// <summary>净化瞬间扩散动画（扩散放大 + 淡出）</summary>
    private class PurifyAnimator : MonoBehaviour
    {
        private float _timer;
        private const float Duration = 0.4f;
        private SpriteRenderer _sr;
        private Material _mat;
        private float _baseOpacity = 0.35f;
        private float _startScale = 1f;
        private float _endScale = 2.8f;

        public void Run(float startScale, float endScale)
        {
            _startScale = startScale;
            _endScale = endScale;

            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
            {
                _mat = _sr.material;
                if (_mat != null)
                    _baseOpacity = _mat.GetFloat("_Opacity");
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / Duration);

            // 扩散放大：从 _startScale 放大到 _endScale
            float scale = Mathf.Lerp(_startScale, _endScale, t);
            transform.localScale = new Vector3(scale, scale, 1f);

            // 淡出：PurifyWave shader 不读 vertex color，须通过材质 _Opacity 淡出
            if (_mat != null)
                _mat.SetFloat("_Opacity", Mathf.Lerp(_baseOpacity, 0f, t));

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
