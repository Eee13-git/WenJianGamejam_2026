using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 扩散波运行时组件：管理波纹扩散、碰撞检测、伤害/冻结/视觉/取消机制。
/// 由 WaveSkillEffect 创建，参数驱动行为。合并了原 TimeStopManager + AxonBlockWave。
/// </summary>
public class WaveRuntime : MonoBehaviour
{
    // ── 配置（由 Initialize 设置）──
    private Vector2 _origin;
    private float _waveSpeed;
    private float _maxRadius;
    private float _damage;
    private float _freezeDuration;
    private bool _freezeProjectiles;
    private bool _cancelOnAttack;
    private bool _cancelOnSkillCast;
    private float _cancelGracePeriod;
    private Material _waveMaterial;
    private Material _overlayMaterial;
    private Projectile.OwnerType _ownerType;

    // ── 运行时状态 ──
    private float _effectTimer;
    private float _effectDuration;
    private float _waveElapsed;
    private bool _isActive;
    private bool _listeningForCancel;

    // 命中实体去重
    private readonly HashSet<int> _hitEntityIds = new();

    // 冻结实体（用于解冻）
    private readonly HashSet<EnemyCore> _frozenEnemies = new();
    private readonly HashSet<Projectile> _frozenProjectiles = new();

    // 玩家引用（取消监听）
    private PlayerCombat _playerCombat;
    private PlayerSkillManager _playerSkillManager;

    // 视觉
    private Material _waveMatInstance;
    private Material _overlayMatInstance;
    private GameObject _overlayObject;

    // 碰撞检测降频
    private float _nextCheckTime;
    private const float CheckInterval = 0.02f;

    /// <summary>是否正在生效中</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 初始化扩散波。所有行为由参数控制，可选功能传 null/0/false 即可禁用。
    /// </summary>
    public void Initialize(
        Vector2 origin, float waveSpeed, float maxRadius,
        float damage, float freezeDuration, bool freezeProjectiles,
        bool cancelOnAttack, bool cancelOnSkillCast, float cancelGracePeriod,
        Material waveMaterial, Material overlayMaterial,
        Projectile.OwnerType ownerType)
    {
        _origin = origin;
        _waveSpeed = waveSpeed;
        _maxRadius = maxRadius;
        _damage = damage;
        _freezeDuration = freezeDuration;
        _freezeProjectiles = freezeProjectiles;
        _cancelOnAttack = cancelOnAttack;
        _cancelOnSkillCast = cancelOnSkillCast;
        _cancelGracePeriod = cancelGracePeriod;
        _waveMaterial = waveMaterial;
        _overlayMaterial = overlayMaterial;
        _ownerType = ownerType;

        // 效果总持续时间：有冻结则等于冻结时长，否则等于波纹扩散时间+0.5s缓冲
        float waveTime = maxRadius / Mathf.Max(waveSpeed, 0.01f);
        _effectDuration = freezeDuration > 0f ? freezeDuration : waveTime + 0.5f;
        _effectTimer = _effectDuration;
        _waveElapsed = 0f;
        _isActive = true;
        _listeningForCancel = false;
        _nextCheckTime = 0f;

        // 创建视觉
        if (_waveMaterial != null) CreateWaveVisual();
        if (_overlayMaterial != null) CreateOverlay();

        // 查找玩家组件（用于取消监听）
        if (_cancelOnAttack || _cancelOnSkillCast)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _playerCombat = playerGO.GetComponent<PlayerCombat>();
                _playerSkillManager = playerGO.GetComponent<PlayerSkillManager>();
            }
            StartCoroutine(EnableCancelListeningAfterDelay(_cancelGracePeriod));
        }
    }

    // ──────────────────────────────────────────────
    //  每帧更新
    // ──────────────────────────────────────────────

    private void Update()
    {
        if (!_isActive)
        {
            // 淡出中
            if (_waveMatInstance != null)
                _waveMatInstance.SetFloat("_Fade",
                    Mathf.MoveTowards(_waveMatInstance.GetFloat("_Fade"), 0f, Time.deltaTime * 4f));
            return;
        }

        _effectTimer -= Time.deltaTime;

        // 波纹扩散
        _waveElapsed += Time.deltaTime;
        float currentRadius = Mathf.Min(_waveElapsed * _waveSpeed, _maxRadius);

        // 更新 shader 参数
        float progress = Mathf.Clamp01((_effectDuration - _effectTimer) / _effectDuration);

        if (_waveMatInstance != null)
        {
            _waveMatInstance.SetFloat("_CurrentRadius", currentRadius);
            _waveMatInstance.SetFloat("_MaxRadius", _maxRadius);
            _waveMatInstance.SetFloat("_Progress", progress);
        }

        if (_overlayMatInstance != null)
        {
            _overlayMatInstance.SetFloat("_Progress", progress);
            _overlayMatInstance.SetVector("_WaveCenter", _origin);
        }

        // 覆盖层跟随相机
        if (_overlayObject != null && Camera.main != null)
        {
            var camPos = Camera.main.transform.position;
            _overlayObject.transform.position = new Vector3(camPos.x, camPos.y, 0f);
        }

        // 碰撞检测（波纹扩散期间）
        if (currentRadius < _maxRadius)
        {
            if (Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + CheckInterval;
                DetectEntities(currentRadius);
            }
        }

        if (_effectTimer <= 0f)
            EndEffect();
    }

    // ──────────────────────────────────────────────
    //  碰撞检测
    // ──────────────────────────────────────────────

    private void DetectEntities(float radius)
    {
        float innerRadius = Mathf.Max(0f, radius - 1f);
        float outerRadius = radius + 1f;
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";

        Collider2D[] hits = Physics2D.OverlapCircleAll(_origin, outerRadius);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            int id = hit.GetInstanceID();
            if (_hitEntityIds.Contains(id)) continue;

            float dist = Vector2.Distance(_origin, hit.transform.position);
            if (dist > outerRadius || dist < innerRadius) continue;

            bool processed = false;

            // 目标阵营实体（伤害+冻结）
            if (hit.CompareTag(targetTag))
            {
                // 伤害（通过 IDamageable 接口，适用于敌人和玩家）
                if (_damage > 0f && hit.TryGetComponent<IDamageable>(out var damageable))
                    damageable.TakeDamage(_damage);

                // 冻结敌人（禁用 AI 组件）
                var enemy = hit.GetComponent<EnemyCore>();
                if (enemy != null && !enemy.IsDead)
                    FreezeEnemy(enemy);

                processed = true;
            }

            // 子弹（不区分阵营，全部冻结）
            if (_freezeProjectiles && _freezeDuration > 0f)
            {
                var proj = hit.GetComponent<Projectile>();
                if (proj != null)
                {
                    FreezeProjectile(proj);
                    processed = true;
                }
            }

            if (processed)
                _hitEntityIds.Add(id);
        }
    }

    // ──────────────────────────────────────────────
    //  冻结 / 解冻
    // ──────────────────────────────────────────────

    private void FreezeEnemy(EnemyCore enemy)
    {
        if (_freezeDuration <= 0f || _frozenEnemies.Contains(enemy)) return;

        if (enemy.Movement != null)
        {
            enemy.Movement.Stop();
            enemy.Movement.enabled = false;
        }
        if (enemy.StateMachine != null)
            enemy.StateMachine.enabled = false;
        if (enemy.SkillManager != null)
            enemy.SkillManager.enabled = false;

        _frozenEnemies.Add(enemy);
    }

    private void FreezeProjectile(Projectile proj)
    {
        if (_freezeDuration <= 0f || _frozenProjectiles.Contains(proj)) return;
        proj.Freeze();
        _frozenProjectiles.Add(proj);
    }

    private void UnfreezeAll()
    {
        foreach (var enemy in _frozenEnemies)
        {
            if (enemy == null) continue;
            if (enemy.StateMachine != null) enemy.StateMachine.enabled = true;
            if (enemy.Movement != null) enemy.Movement.enabled = true;
            if (enemy.SkillManager != null) enemy.SkillManager.enabled = true;
        }

        foreach (var proj in _frozenProjectiles)
        {
            if (proj != null) proj.Unfreeze();
        }

        _frozenEnemies.Clear();
        _frozenProjectiles.Clear();
    }

    // ──────────────────────────────────────────────
    //  取消机制
    // ──────────────────────────────────────────────

    private IEnumerator EnableCancelListeningAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isActive) yield break;

        _listeningForCancel = true;
        if (_cancelOnAttack && _playerCombat != null)
            _playerCombat.OnShoot += OnPlayerAttack;
        if (_cancelOnSkillCast && _playerSkillManager != null)
            _playerSkillManager.OnSkillCast += OnPlayerSkillCast;
    }

    private void OnPlayerAttack(Vector2 dir, float spd, float dmg)
    {
        if (_listeningForCancel) EndEffect();
    }

    private void OnPlayerSkillCast(int slot, SkillData data)
    {
        if (_listeningForCancel) EndEffect();
    }

    // ──────────────────────────────────────────────
    //  结束 / 清理
    // ──────────────────────────────────────────────

    private void EndEffect()
    {
        if (!_isActive) return;

        if (_playerCombat != null)
            _playerCombat.OnShoot -= OnPlayerAttack;
        if (_playerSkillManager != null)
            _playerSkillManager.OnSkillCast -= OnPlayerSkillCast;

        _isActive = false;
        _listeningForCancel = false;

        UnfreezeAll();
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float fadeTime = 0.4f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = 1f - elapsed / fadeTime;

            if (_waveMatInstance != null)
                _waveMatInstance.SetFloat("_Fade", t);
            if (_overlayMatInstance != null)
                _overlayMatInstance.SetFloat("_Progress", t * 0.3f);

            yield return null;
        }

        Destroy(gameObject);
    }

    // ──────────────────────────────────────────────
    //  视觉创建
    // ──────────────────────────────────────────────

    private void CreateWaveVisual()
    {
        var waveGO = new GameObject("WaveVisual");
        waveGO.transform.SetParent(transform);
        waveGO.transform.position = _origin;

        var renderer = waveGO.AddComponent<SpriteRenderer>();
        _waveMatInstance = new Material(_waveMaterial);
        renderer.material = _waveMatInstance;
        renderer.sortingOrder = 100;
        renderer.color = Color.white;

        float scale = _maxRadius * 2f;
        waveGO.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void CreateOverlay()
    {
        var cam = Camera.main;
        if (cam == null) return;

        _overlayObject = new GameObject("WaveOverlay");
        _overlayObject.transform.SetParent(transform);

        var renderer = _overlayObject.AddComponent<MeshRenderer>();
        renderer.sortingOrder = 200;

        var filter = _overlayObject.AddComponent<MeshFilter>();
        filter.mesh = CreateFullscreenQuad(cam);

        _overlayMatInstance = new Material(_overlayMaterial);
        renderer.material = _overlayMatInstance;
        _overlayMatInstance.SetFloat("_Progress", 0f);

        _overlayObject.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        _overlayObject.transform.rotation = cam.transform.rotation;
    }

    private static Mesh CreateFullscreenQuad(Camera cam)
    {
        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;

        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new(-width / 2f, -height / 2f, 1f),
            new( width / 2f, -height / 2f, 1f),
            new( width / 2f,  height / 2f, 1f),
            new(-width / 2f,  height / 2f, 1f),
        };
        mesh.uv = new Vector2[]
        {
            new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f),
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private void OnDestroy()
    {
        if (_isActive)
        {
            if (_playerCombat != null)
                _playerCombat.OnShoot -= OnPlayerAttack;
            if (_playerSkillManager != null)
                _playerSkillManager.OnSkillCast -= OnPlayerSkillCast;
            UnfreezeAll();
        }
    }
}
