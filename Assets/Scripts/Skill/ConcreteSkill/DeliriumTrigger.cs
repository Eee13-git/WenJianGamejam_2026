using System.Collections;
using UnityEngine;

/// <summary>
/// 急性谵妄触发体 — 由 ProjectileSkillEffect(speed=0) 原地生成。
/// Start 时给当前房间所有存活敌人挂上混乱 debuff，播放扩散环视觉后销毁。
/// 可选：同时锁定玩家输入（affectPlayer=true）。
/// "释放后即死"由外部 buff（InstantDeathBuff）实现，不在 Trigger 内硬编码。
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

    [Header("扩展选项")]
    [Tooltip("是否同时影响玩家（锁定玩家输入）")]
    [SerializeField] private bool _affectPlayer = false;
    [Tooltip("玩家锁定时长（秒）")]
    [SerializeField] private float _playerLockDuration = 3f;

    [Header("机制成长（升级可选）")]
    [Tooltip("每级额外玩家锁定时长（秒，0=不成长）")]
    [SerializeField] private float _playerLockDurationPerLevel = 0f;
    [Tooltip("每级额外混乱 debuff 时长（秒，0=不成长）")]
    [SerializeField] private float _buffDurationPerLevel = 0f;

    private SpriteRenderer _sr;
    private Material _matInstance;
    private float _debuffDurationOverride = -1f;

    /// <summary>按技能等级成长玩家锁定时长/混乱 buff 时长（level<=1 时无变化）</summary>
    public void ApplyLevel(int level)
    {
        if (level <= 1) return;
        if (_playerLockDurationPerLevel > 0f)
            _playerLockDuration = _playerLockDuration + (level - 1) * _playerLockDurationPerLevel;
        if (_buffDurationPerLevel > 0f && _deliriumDebuff != null)
            _debuffDurationOverride = _deliriumDebuff.duration + (level - 1) * _buffDurationPerLevel;
    }

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

        // 机制成长：玩家锁定时长/混乱 debuff 时长随技能等级提升
        if (proj != null)
            ApplyLevel(proj.Level);

        // 给当前房间所有存活敌人挂 debuff
        ApplyDebuffToRoom(casterGO);

        // 同时影响玩家（锁定输入）
        if (_affectPlayer)
            StartCoroutine(AffectPlayer());

        // 播放扩散环视觉后销毁
        StartCoroutine(PlayWaveAndDestroy());
    }

    /// <summary>锁定玩家输入若干秒</summary>
    private IEnumerator AffectPlayer()
    {
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.InputLocked = true;
            yield return new WaitForSeconds(_playerLockDuration);
            if (player != null) player.InputLocked = false;
        }
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

            var buff = buffMgr.ApplyBuff(_deliriumDebuff, caster);
            // 机制成长：混乱 debuff 时长覆盖（Refresh 叠加时保持升级后时长）
            if (buff != null && _debuffDurationOverride > 0f)
                buff.SetDurationOverride(_debuffDurationOverride);
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
