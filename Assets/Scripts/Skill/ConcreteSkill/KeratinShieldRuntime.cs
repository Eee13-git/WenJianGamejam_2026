using UnityEngine;

/// <summary>
/// 角质增生覆膜护盾运行时组件 — 挂在施法者身上。
/// 架盾期间：不可攻击（AttackLocked）、移速降低（SpeedMultiplier）、
/// 可抵挡弹幕伤害（Projectile 拦截）和冲撞伤害（MeleeAttack 拦截）。
/// 释放后 perfectWindow 秒内被击中 → 完美格挡 → 立即刷新该技能冷却。
/// 玩家长按技能键持续架盾（松开提前结束）；敌人自动持续满时长。
/// </summary>
public class KeratinShieldRuntime : MonoBehaviour
{
    // ── 配置（由 Activate 设置）──
    private float _duration;
    private float _perfectWindow;
    private float _slowFactor;
    private float _immuneDuration;   // 格挡后无敌时长
    private string _skillId;
    private KeyCode _holdKey = KeyCode.None;

    // ── 运行时状态 ──
    private float _timer;
    private float _perfectTimer;
    private bool _isActive;
    private bool _perfectTriggered;
    private bool _breaking;         // 破碎动画中（已挡一次，短暂显示破碎后销毁）
    private float _breakTimer;

    // ── 施法者引用 ──
    private GameObject _casterGO;
    private PlayerController _playerController;
    private EnemyMovement _enemyMovement;
    private ISkillCaster _caster;

    // ── 视觉 ──
    private SpriteRenderer _visualRenderer;
    private Material _matInstance;
    private float _originalEnemyMoveSpeed;
    private float _shieldScale = 1f;        // 当前护盾缩放（激活时扩散动画）
    private float _hitFlash;                 // 被击中闪白强度（0~1）
    private float _appearTime;               // 激活扩散动画进度

    /// <summary>是否正在架盾</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 激活护盾。
    /// </summary>
    public void Activate(ISkillCaster caster, float duration, float slowFactor,
        float perfectWindow, float immuneDuration, string skillId, Material shieldMaterial)
    {
        _caster = caster;
        _casterGO = caster.CasterTransform.gameObject;
        _duration = duration;
        _slowFactor = Mathf.Clamp(slowFactor, 0.1f, 1f);
        _perfectWindow = perfectWindow;
        _immuneDuration = immuneDuration;
        _skillId = skillId;

        _timer = duration;
        _perfectTimer = perfectWindow;
        _isActive = true;
        _perfectTriggered = false;
        _shieldScale = 0.5f;   // 激活扩散动画起点
        _hitFlash = 0f;
        _appearTime = 0f;

        _playerController = _casterGO.GetComponent<PlayerController>();
        _enemyMovement = _casterGO.GetComponent<EnemyMovement>();

        // 架盾限制：玩家锁攻击 + 减速；敌人禁移动 + 减速
        if (_playerController != null)
        {
            _playerController.AttackLocked = true;
            _playerController.SpeedMultiplier = _slowFactor;
        }
        if (_enemyMovement != null)
        {
            _originalEnemyMoveSpeed = _enemyMovement.MoveSpeed;
            _enemyMovement.MoveSpeed = _originalEnemyMoveSpeed * _slowFactor;
            _enemyMovement.enabled = true; // 敌人仍可缓慢移动（架盾走路）
        }

        // 玩家长按检测：找到本技能绑定的键
        if (_playerController != null)
        {
            var psm = _casterGO.GetComponent<PlayerSkillManager>();
            if (psm != null)
                _holdKey = psm.GetKeyCodeBySkillId(_skillId);
        }

        CreateVisual(shieldMaterial);
    }

    // ──────────────────────────────────────────────
    //  每帧更新
    // ──────────────────────────────────────────────

    private void Update()
    {
        // 破碎动画中：放大 + 闪白淡出后销毁
        if (_breaking)
        {
            _breakTimer += Time.deltaTime;
            float t = _breakTimer / 0.15f;

            if (_visualRenderer != null)
            {
                // 快速放大（破碎爆开）+ 闪白
                float scale = _shieldScale * 2f * (1f + t * 1.5f);
                _visualRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                if (_matInstance != null)
                {
                    _matInstance.SetFloat("_HitFlash", 1f - t);
                    _matInstance.SetFloat("_Break", t);
                }
            }

            if (t >= 1f)
            {
                // 恢复状态后再销毁组件（护盾已消耗）
                RestoreCaster();
                DestroyVisual();
                Destroy(this);
            }
            return;
        }

        if (!_isActive) return;

        _timer -= Time.deltaTime;
        _perfectTimer -= Time.deltaTime;

        // 视觉跟随施法者 + 更新动画
        if (_visualRenderer != null)
        {
            transform.position = _casterGO.transform.position;

            // 激活扩散动画：0.2s 内从 0.5x 弹性放大到 1x
            _appearTime += Time.deltaTime;
            float t = Mathf.Clamp01(_appearTime / 0.2f);
            float ease = 1f - Mathf.Pow(1f - t, 3f);   // easeOutCubic
            float overshoot = 1f + 0.15f * Mathf.Sin(t * Mathf.PI); // 弹性过冲
            _shieldScale = Mathf.Lerp(0.5f, 1f, ease) * overshoot;

            // 命中闪白衰减
            if (_hitFlash > 0f)
                _hitFlash = Mathf.MoveTowards(_hitFlash, 0f, Time.deltaTime * 4f);

            // 应用缩放 + 闪白
            float scale = _shieldScale * 2f; // sprite 尺寸基准
            _visualRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            if (_matInstance != null)
                _matInstance.SetFloat("_HitFlash", _hitFlash);
        }

        // 玩家长按检测：松开技能键提前结束架盾
        if (_playerController != null && _holdKey != KeyCode.None)
        {
            if (!Input.GetKey(_holdKey))
            {
                EndShield();
                return;
            }
        }

        if (_timer <= 0f)
            EndShield();
    }

    // ──────────────────────────────────────────────
    //  拦截入口（由 Projectile / MeleeAttack 调用）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 尝试拦截一次伤害。返回 true = 已挡住（调用方不再造成伤害）。
    /// 架盾只可抵挡一次攻击：任何一次成功拦截都会结束架盾（护盾破碎）。
    /// 完美窗口内被击中 → 额外触发完美格挡（刷新冷却）。
    /// </summary>
    public bool TryBlock()
    {
        if (!_isActive || _breaking) return false;

        bool perfect = _perfectTimer > 0f && !_perfectTriggered;

        // 挡住瞬间立即恢复施法者状态（解锁攻击+移速），破碎动画只是视觉
        RestoreCaster();

        // 格挡后授予无敌帧（期间不受到任何伤害）
        if (_immuneDuration > 0f)
        {
            var immunity = _casterGO.GetComponent<DamageImmunity>();
            if (immunity == null)
                immunity = _casterGO.AddComponent<DamageImmunity>();
            immunity.GrantImmunity(_immuneDuration);
        }

        // 命中反馈：闪白 + 破碎动画
        _hitFlash = 1f;
        _breaking = true;
        _breakTimer = 0f;
        _isActive = false;   // 已消耗，不再拦截

        if (perfect)
        {
            _perfectTriggered = true;
            ResetSkillCooldown();
        }

        return true;
    }
    /// <summary>刷新本技能冷却（完美格挡奖励）</summary>
    private void ResetSkillCooldown()
    {
        if (_casterGO == null || string.IsNullOrEmpty(_skillId)) return;

        var psm = _casterGO.GetComponent<PlayerSkillManager>();
        if (psm != null)
        {
            psm.ResetCooldownBySkillId(_skillId);
            return;
        }

        var esm = _casterGO.GetComponent<EnemySkillManager>();
        if (esm != null)
            esm.ResetCooldownBySkillId(_skillId);
    }

    // ──────────────────────────────────────────────
    //  视觉
    // ──────────────────────────────────────────────

    private void CreateVisual(Material shieldMaterial)
    {
        var go = new GameObject("KeratinShieldVisual");
        go.transform.SetParent(transform);
        go.transform.position = _casterGO.transform.position;

        _visualRenderer = go.AddComponent<SpriteRenderer>();
        if (shieldMaterial != null)
        {
            _matInstance = new Material(shieldMaterial);
            _visualRenderer.material = _matInstance;
        }
        _visualRenderer.sprite = CreateWhiteSprite();
        _visualRenderer.sortingOrder = 90;
        go.transform.localScale = Vector3.one;
    }

    /// <summary>生成纯白 1x1 sprite（PPU=1）</summary>
    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    // ──────────────────────────────────────────────
    //  结束 / 清理
    // ──────────────────────────────────────────────

    private void EndShield()
    {
        if (!_isActive && !_breaking) return;

        _isActive = false;
        RestoreCaster();
        DestroyVisual();
        Destroy(this);
    }

    /// <summary>恢复施法者状态（解锁攻击 + 恢复移速）</summary>
    private void RestoreCaster()
    {
        if (_playerController != null)
        {
            _playerController.AttackLocked = false;
            _playerController.SpeedMultiplier = 1f;
        }
        if (_enemyMovement != null)
            _enemyMovement.MoveSpeed = _originalEnemyMoveSpeed;
    }

    /// <summary>销毁视觉子对象</summary>
    private void DestroyVisual()
    {
        if (_visualRenderer != null && _visualRenderer.gameObject != null)
            Destroy(_visualRenderer.gameObject);
        _visualRenderer = null;
    }

    private void OnDestroy()
    {
        if (_isActive || _breaking)
        {
            RestoreCaster();
        }
        // 兜底销毁视觉
        if (_visualRenderer != null && _visualRenderer.gameObject != null && _visualRenderer.gameObject != gameObject)
            Destroy(_visualRenderer.gameObject);
    }
}
