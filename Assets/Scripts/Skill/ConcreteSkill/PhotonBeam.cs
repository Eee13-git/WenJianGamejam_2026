using System.Collections;
using UnityEngine;

/// <summary>
/// 线粒体生物光子辐散 — 血色光柱组件。
/// 3 阶段特效：
///   1. 预警：落点出现血色瞄准环（收缩脉冲）
///   2. 喷射：血柱从地面向上涌出（生物爆发感）
///   3. 命中：落地闪光 + 血池 + 一次性伤害
/// </summary>
public class PhotonBeam : MonoBehaviour
{
    // ════════════════════════════════════════════
    //  预警环
    // ════════════════════════════════════════════
    [Header("──── 预警环 ────")]
    [Tooltip("预警持续时长（秒）")]
    [SerializeField] private float _warnDuration = 0.5f;
    [Tooltip("预警环收缩起始倍数")]
    [SerializeField] private float _warnStartScale = 1.4f;
    [Tooltip("预警环脉冲速度（越高闪烁越快）")]
    [SerializeField] private float _warnPulseSpeed = 20f;
    [Tooltip("预警闪白强度")]
    [SerializeField] private float _warnFlashIntensity = 0.5f;

    // ════════════════════════════════════════════
    //  光柱
    // ════════════════════════════════════════════
    [Header("──── 光柱 ────")]
    [Tooltip("光柱高度（世界单位）")]
    [SerializeField] private float _beamHeight = 8f;
    [Tooltip("光柱宽度系数（相对伤害半径）")]
    [SerializeField] private float _beamWidthFactor = 0.5f;
    [Tooltip("光柱生长时长（秒），从地面向上喷射）")]
    [SerializeField] private float _beamGrowTime = 0.14f;
    [Tooltip("光柱生长时闪白强度")]
    [SerializeField] private float _beamGrowFlash = 0.6f;
    [Tooltip("光束/预警环/闪光材质（PhotonBeamCore）")]
    [SerializeField] private Material _beamMaterial;

    // ════════════════════════════════════════════
    //  落地特效
    // ════════════════════════════════════════════
    [Header("──── 落地特效 ────")]
    [Tooltip("落地闪光持续时长（秒）")]
    [SerializeField] private float _impactFlashDuration = 0.25f;
    [Tooltip("闪光扩散起始倍数（×半径）")]
    [SerializeField] private float _impactFlashStartScale = 0.8f;
    [Tooltip("闪光扩散终点倍数（×半径）")]
    [SerializeField] private float _impactFlashEndScale = 2.5f;
    [Tooltip("落地血池材质（PhotonBloodPool）")]
    [SerializeField] private Material _poolMaterial;
    [Tooltip("血池半径")]
    [SerializeField] private float _poolRadius = 1.5f;
    [Tooltip("血池持续时间（秒）")]
    [SerializeField] private float _poolDuration = 10f;
    [Tooltip("血池持续伤害 debuff")]
    [SerializeField] private BuffData _radianceDebuff;

    // ════════════════════════════════════════════
    //  伤害
    // ════════════════════════════════════════════
    [Header("──── 伤害 ────")]
    [Tooltip("伤害判定半径")]
    [SerializeField] private float _radius = 1.2f;
    [Tooltip("单段基础伤害（再加施法者攻击力）")]
    [SerializeField] private float _baseDamage = 20f;

    // ── 运行时状态 ──
    private Projectile.OwnerType _ownerType;
    private float _attackStrength;

    // ── 视觉子对象 ──
    private SpriteRenderer _warningRing;
    private Material _warningMat;
    private SpriteRenderer _beamSprite;
    private Material _beamMat;

    /// <summary>初始化光柱</summary>
    public void Initialize(Projectile.OwnerType ownerType, float attackStrength)
    {
        _ownerType = ownerType;
        _attackStrength = attackStrength;
    }

    private void Start()
    {
        CreateVisuals();
        StartCoroutine(BeamSequence());
    }

    // ──────────────────────────────────────────────
    //  视觉构建
    // ──────────────────────────────────────────────

    private void CreateVisuals()
    {
        Material baseMat = _beamMaterial;
        if (baseMat == null)
            baseMat = GetComponent<SpriteRenderer>()?.sharedMaterial;

        // 1. 预警瞄准环
        var warnGO = new GameObject("WarningRing");
        warnGO.transform.SetParent(transform, false);
        warnGO.transform.localPosition = Vector3.zero;
        warnGO.transform.localScale = new Vector3(_radius * 2f, _radius * 2f, 1f);

        _warningRing = warnGO.AddComponent<SpriteRenderer>();
        if (baseMat != null)
        {
            _warningMat = new Material(baseMat);
            _warningRing.material = _warningMat;
            _warningMat.SetFloat("_Mode", 0f);
        }
        _warningRing.sprite = CreateWhiteSprite();
        _warningRing.sortingOrder = 30;
        _warningRing.enabled = false;

        // 2. 血柱（底部贴地，向上延伸）
        var beamGO = new GameObject("BeamCore");
        beamGO.transform.SetParent(transform, false);
        float fullWidth = _radius * _beamWidthFactor;
        float fullHeight = _beamHeight;
        beamGO.transform.localPosition = new Vector3(0f, fullHeight * 0.5f, 0f);
        beamGO.transform.localScale = new Vector3(fullWidth, fullHeight, 1f);

        _beamSprite = beamGO.AddComponent<SpriteRenderer>();
        if (baseMat != null)
        {
            _beamMat = new Material(baseMat);
            _beamSprite.material = _beamMat;
            _beamMat.SetFloat("_Mode", 1f);
        }
        _beamSprite.sprite = CreateWhiteSprite();
        _beamSprite.sortingOrder = 40;
        _beamSprite.enabled = false;

        // 清理主对象 SpriteRenderer
        var mainSr = GetComponent<SpriteRenderer>();
        if (mainSr != null)
            mainSr.enabled = false;
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    // ──────────────────────────────────────────────
    //  3 阶段序列
    // ──────────────────────────────────────────────

    private IEnumerator BeamSequence()
    {
        // ── 阶段 1：预警 ──
        _warningRing.enabled = true;
        float warnTimer = 0f;
        while (warnTimer < _warnDuration)
        {
            warnTimer += Time.deltaTime;
            float t = warnTimer / _warnDuration;

            float pulse = 1f + 0.15f * Mathf.Sin(t * _warnPulseSpeed);
            float scale = Mathf.Lerp(_warnStartScale, 1f, t) * pulse;
            _warningRing.transform.localScale = new Vector3(
                _radius * 2f * scale, _radius * 2f * scale, 1f);

            if (_warningMat != null)
                _warningMat.SetFloat("_Flash",
                    _warnFlashIntensity + _warnFlashIntensity * Mathf.Sin(t * _warnPulseSpeed));

            yield return null;
        }

        // ── 阶段 2：喷射 ──
        _warningRing.enabled = false;
        _beamSprite.enabled = true;

        float fullWidth = _radius * _beamWidthFactor;
        float fullHeight = _beamHeight;
        Vector3 finalPos = new Vector3(0f, fullHeight * 0.5f, 0f);
        Vector3 fullScale = new Vector3(fullWidth, fullHeight, 1f);

        float growTimer = 0f;
        while (growTimer < _beamGrowTime)
        {
            growTimer += Time.deltaTime;
            float t = Mathf.Clamp01(growTimer / _beamGrowTime);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            float sy = Mathf.Lerp(0.04f, 1f, ease);
            float sx = Mathf.Lerp(0f, 1f, ease);
            _beamSprite.transform.localScale = new Vector3(fullWidth * sx, fullHeight * sy, 1f);
            _beamSprite.transform.localPosition = new Vector3(0f, fullHeight * sy * 0.5f, 0f);

            if (_beamMat != null)
                _beamMat.SetFloat("_Flash", Mathf.Lerp(_beamGrowFlash, 0f, t));

            yield return null;
        }
        _beamSprite.transform.localScale = fullScale;
        _beamSprite.transform.localPosition = finalPos;
        if (_beamMat != null)
            _beamMat.SetFloat("_Flash", 0f);

        // 落地音效（光柱砸下）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("photon_radiance_landing");

        // 落地闪光 + 血池
        SpawnImpactFlash();
        SpawnRadiancePool();

        // ── 阶段 3：命中伤害 ──
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        float damage = _baseDamage + _attackStrength;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _radius, ~0);
        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;
            var enemy = hit.GetComponent<EnemyCore>();
            if (enemy != null && enemy.IsDead) continue;
            if (hit.TryGetComponent<IDamageable>(out var d))
                d.TakeDamage(damage);
        }

        // 命中脉冲
        if (_beamMat != null)
            _beamMat.SetFloat("_Flash", 1f);
        _beamSprite.transform.localScale = fullScale * 1.1f;

        yield return new WaitForSeconds(0.15f);
        _beamSprite.transform.localScale = fullScale;
        if (_beamMat != null)
            _beamMat.SetFloat("_Flash", 0f);

        yield return new WaitForSeconds(0.2f);
        Destroy(gameObject);
    }

    // ──────────────────────────────────────────────
    //  落地血池
    // ──────────────────────────────────────────────

    private void SpawnRadiancePool()
    {
        var poolGO = new GameObject("RadiancePool");
        poolGO.transform.position = transform.position;

        var sr = poolGO.AddComponent<SpriteRenderer>();
        if (_poolMaterial != null)
        {
            sr.material = new Material(_poolMaterial);
        }
        else if (_beamMaterial != null)
        {
            var mat = new Material(_beamMaterial);
            mat.SetFloat("_Mode", 0f);
            sr.material = mat;
        }
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 30;

        var pool = poolGO.AddComponent<BloodPool>();
        pool.Initialize(_ownerType, gameObject, _poolRadius, _poolDuration);

        if (_radianceDebuff != null)
            pool.SetDebuff(_radianceDebuff);
    }

    // ──────────────────────────────────────────────
    //  落地闪光
    // ──────────────────────────────────────────────

    private void SpawnImpactFlash()
    {
        var flashGO = new GameObject("ImpactFlash");
        flashGO.transform.SetParent(transform, false);
        flashGO.transform.localPosition = Vector3.zero;

        var sr = flashGO.AddComponent<SpriteRenderer>();
        if (_beamMaterial != null)
        {
            sr.material = new Material(_beamMaterial);
            sr.material.SetFloat("_Mode", 2f);
        }
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 35;

        var anim = flashGO.AddComponent<ImpactFlashAnimator>();
        anim.Init(_radius, _impactFlashDuration, _impactFlashStartScale, _impactFlashEndScale);
    }

    /// <summary>落地闪光扩散动画</summary>
    private class ImpactFlashAnimator : MonoBehaviour
    {
        private float _radius;
        private float _duration;
        private float _startScale;
        private float _endScale;
        private float _timer;
        private SpriteRenderer _sr;
        private Material _mat;

        public void Init(float radius, float duration, float startScale, float endScale)
        {
            _radius = radius;
            _duration = duration;
            _startScale = startScale;
            _endScale = endScale;
            _sr = GetComponent<SpriteRenderer>();
            _mat = _sr.material;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);

            float scale = Mathf.Lerp(_radius * _startScale, _radius * _endScale, t);
            transform.localScale = new Vector3(scale * 2f, scale * 2f, 1f);

            if (_mat != null)
                _mat.SetFloat("_Flash", 1f - t);

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
