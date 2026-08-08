using System.Collections;
using UnityEngine;

/// <summary>
/// 急性谵妄触发体 — 由 ProjectileSkillEffect(speed=0) 原地生成。
/// Start 时给当前房间所有存活敌人挂上混乱 debuff，播放扩散环视觉后销毁。
/// </summary>
public class DeliriumTrigger : MonoBehaviour
{
    [Header("触发配置")]
    [Tooltip("要施加的混乱 debuff（DeliriumBuff 效果）")]
    [SerializeField] private BuffData _deliriumDebuff;
    [Tooltip("扩散环最大半径（视觉）")]
    [SerializeField] private float _waveRadius = 10f;
    [Tooltip("扩散动画时长（秒）")]
    [SerializeField] private float _waveDuration = 0.8f;

    private SpriteRenderer _sr;
    private Material _matInstance;

    private void Awake()
    {
        // 禁用 Projectile（原地不动、不触发命中）
        var proj = GetComponent<Projectile>();
        if (proj != null) proj.enabled = false;

        // 修复 sprite 引用丢失（运行时创建纹理序列化丢失）
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && _sr.sprite == null)
            _sr.sprite = CreateWhiteSprite();
        if (_sr != null)
        {
            _matInstance = new Material(_sr.sharedMaterial);
            _sr.material = _matInstance;
        }
    }

    private void Start()
    {
        var proj = GetComponent<Projectile>();
        GameObject casterGO = proj != null ? proj.Caster : null;

        // 给当前房间所有存活敌人挂 debuff
        ApplyDebuffToRoom(casterGO);

        // 播放扩散环视觉后销毁
        StartCoroutine(PlayWaveAndDestroy());
    }

    /// <summary>给场景中所有存活、未同化的敌人挂混乱 debuff</summary>
    private void ApplyDebuffToRoom(GameObject caster)
    {
        if (_deliriumDebuff == null) return;

        var enemies = FindObjectsOfType<EnemyCore>();
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || enemy.IsAssimilated) continue;

            var buffMgr = enemy.GetComponent<BuffManager>();
            if (buffMgr == null) continue;

            buffMgr.ApplyBuff(_deliriumDebuff, caster);
        }

        Debug.Log($"[DeliriumTrigger] 已对 {enemies.Length} 个敌人施加混乱 debuff");
    }

    /// <summary>扩散环视觉动画（复用 DiffusionWave shader 的 _CurrentRadius 参数）</summary>
    private IEnumerator PlayWaveAndDestroy()
    {
        float elapsed = 0f;
        while (elapsed < _waveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _waveDuration);

            if (_matInstance != null)
            {
                _matInstance.SetFloat("_CurrentRadius", t * _waveRadius);
                _matInstance.SetFloat("_MaxRadius", _waveRadius);
                _matInstance.SetFloat("_Fade", 1f - t);
            }

            // 扩散环放大到最大半径
            if (_sr != null)
            {
                float scale = Mathf.Lerp(1f, _waveRadius * 2f, t);
                transform.localScale = new Vector3(scale, scale, 1f);
            }

            yield return null;
        }

        Destroy(gameObject);
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
