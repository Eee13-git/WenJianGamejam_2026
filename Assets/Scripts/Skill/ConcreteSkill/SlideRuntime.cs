using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 泛用滑移/冲刺运行时组件 — 挂在施法者身上，朝移动方向冲刺位移一段距离，期间无敌。
/// 玩家阵营读取 PlayerController.MoveDirection（无输入时朝鼠标方向）；
/// 敌人阵营随机方向。
/// 可选"卷起敌人"能力：位移途中卷起途经的敌人（吸附跟随 + 命中伤害），
/// 被卷起的敌人撞到墙/障碍后受到二次伤害并释放。
/// 泛用位移组件：距离、速度、无敌时长、拖影材质、卷起能力均可配置。
/// </summary>
public class SlideRuntime : MonoBehaviour
{
    // ── 配置（由 Activate 设置）──
    private float _distance;
    private float _speed;
    private float _immuneDuration;
    private Material _trailMaterial;

    // ── 卷起能力配置（由 EnableCarrying 设置）──
    private bool _carryEnabled;
    private float _carryRadius;
    private float _carryDamage;
    private float _wallDamage;
    private bool _carriedInvincible;
    private Projectile.OwnerType _ownerType;

    // ── 爆发冲击配置（由 EnableBurst 设置）──
    private bool _autoAimNearest;      // 自动朝最近敌人突进
    private bool _burstOnHit;          // 命中敌人后释放气浪推开周边
    private float _burstRadius;
    private float _burstPushForce;
    private Material _burstWaveMaterial;
    private bool _burstTriggered;      // 本次冲刺是否已触发过爆发

    // ── 运行时状态 ──
    private Vector2 _direction;
    private Vector2 _forcedDirection;   // 外部强制方向（Boss 轴向冲刺等），为零则内部计算
    private Vector3 _startPos;
    private float _traveled;
    private bool _isActive;
    private float _trailTimer;

    // ── 施法者引用 ──
    private GameObject _casterGO;
    private Rigidbody2D _casterRb;
    private PlayerController _playerController;
    private SpriteRenderer _casterSprite;
    private RigidbodyType2D _originalBodyType;   // 冲刺前刚体类型（结束恢复）
    private Collider2D[] _casterColliders;        // 冲刺期间禁用的施法者 collider

    // ── 卷起的敌人 ──
    private readonly List<CarriedEnemy> _carried = new List<CarriedEnemy>();

    private class CarriedEnemy
    {
        public EnemyCore enemy;
        public Collider2D col;
        public Rigidbody2D rb;
        public bool wallHit;   // 是否已撞墙（触发二次伤害）
    }

    /// <summary>是否冲刺中</summary>
    public bool IsActive => _isActive;

    /// <summary>物理碰撞撞墙（MovePosition 撞墙时触发）</summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isActive || !_carryEnabled) return;
        if (collision.collider == null) return;
        if (collision.collider.CompareTag("Obstacles") || collision.collider.CompareTag("Wall"))
            HitWall();
    }

    /// <summary>激活滑移冲刺</summary>
    public void Activate(ISkillCaster caster, float distance, float speed,
        float immuneDuration, Material trailMaterial)
    {
        _casterGO = caster.CasterTransform.gameObject;
        _distance = Mathf.Max(distance, 0.1f);
        _speed = Mathf.Max(speed, 1f);
        _immuneDuration = immuneDuration;
        _trailMaterial = trailMaterial;

        _casterRb = _casterGO.GetComponent<Rigidbody2D>();
        _playerController = _casterGO.GetComponent<PlayerController>();
        _casterSprite = _casterGO.GetComponentInChildren<SpriteRenderer>();

        // 方向：优先使用外部强制方向（Boss 轴向冲刺等），否则内部计算
        if (_forcedDirection.sqrMagnitude > 0.01f)
        {
            _direction = _forcedDirection.normalized;
        }
        else if (_autoAimNearest)
        {
            _direction = FindNearestTargetDirection();
        }
        else if (_playerController != null)
        {
            Vector2 moveDir = _playerController.MoveDirection;
            if (moveDir.sqrMagnitude > 0.01f)
            {
                _direction = moveDir;
            }
            else if (Camera.main != null)
            {
                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                _direction = ((Vector2)(mouseWorld - _casterGO.transform.position)).normalized;
            }
            if (_direction.sqrMagnitude < 0.01f)
                _direction = Vector2.right;
        }
        else
        {
            // 敌人：随机方向
            Vector2 randomDir = Random.insideUnitCircle;
            _direction = randomDir.sqrMagnitude > 0.01f ? randomDir.normalized : Vector2.right;
        }

        _startPos = _casterGO.transform.position;
        _traveled = 0f;
        _isActive = true;
        _trailTimer = 0f;
        _burstTriggered = false;

        // ── 关键：冲刺期间把施法者刚体改为 Kinematic 并禁用自身 collider ──
        // Dynamic 刚体 MovePosition 撞到其他 collider 会被物理反弹推开（乱窜）；
        // Kinematic 不会被 Dynamic 弹开、撞墙会停在原地，实现"碰撞后停留在原地"。
        // 但 Kinematic + 玩家 collider 与场景静态 collider 重叠会阻塞 MovePosition，
        // 因此同时禁用自身 collider（命中敌人靠 CheckCarry 检测、撞墙靠位置变化检测）。
        if (_casterRb != null)
        {
            _originalBodyType = _casterRb.bodyType;
            _casterRb.bodyType = RigidbodyType2D.Kinematic;
            _casterRb.velocity = Vector2.zero;
        }
        _casterColliders = _casterGO.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in _casterColliders)
            if (c != null) c.enabled = false;

        // 冲刺期间无敌
        if (_immuneDuration > 0f)
        {
            var immunity = _casterGO.GetComponent<DamageImmunity>();
            if (immunity == null)
                immunity = _casterGO.AddComponent<DamageImmunity>();
            immunity.GrantImmunity(_immuneDuration);
        }
    }

    /// <summary>设置强制方向（Boss 轴向冲刺等场景），Activate 时优先使用此方向</summary>
    public void SetForcedDirection(Vector2 dir)
    {
        _forcedDirection = dir;
    }

    /// <summary>启用卷起敌人能力（肌束应急奔突等）</summary>
    public void EnableCarrying(float carryRadius, float carryDamage,
        float wallDamage, bool carriedInvincible, Projectile.OwnerType ownerType)
    {
        _carryEnabled = true;
        _carryRadius = carryRadius;
        _carryDamage = carryDamage;
        _wallDamage = wallDamage;
        _carriedInvincible = carriedInvincible;
        _ownerType = ownerType;
    }

    /// <summary>启用爆发冲击能力（包膜爆发冲击等）：自动锁敌 + 命中释放气浪</summary>
    public void EnableBurst(bool autoAimNearest, bool burstOnHit, float burstRadius,
        float burstPushForce, Material burstWaveMaterial, Projectile.OwnerType ownerType)
    {
        _autoAimNearest = autoAimNearest;
        _burstOnHit = burstOnHit;
        _burstRadius = burstRadius;
        _burstPushForce = burstPushForce;
        _burstWaveMaterial = burstWaveMaterial;
        _ownerType = ownerType;
    }

    /// <summary>朝最近的敌方单位方向（自动锁敌突进）</summary>
    private Vector2 FindNearestTargetDirection()
    {
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        float searchRadius = Mathf.Max(_distance * 2f, 10f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(_casterGO.transform.position, searchRadius, ~0);
        Transform nearest = null;
        float bestSqr = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;

            // 跳过死亡/已同化的敌人
            var enemy = hit.GetComponent<EnemyCore>();
            if (enemy != null && (enemy.IsDead || enemy.IsAssimilated)) continue;

            float sqr = (hit.transform.position - _casterGO.transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = hit.transform;
            }
        }

        if (nearest == null)
        {
            // 无目标：退回默认方向
            if (_playerController != null && Camera.main != null)
            {
                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                Vector2 dir = ((Vector2)(mouseWorld - _casterGO.transform.position)).normalized;
                return dir.sqrMagnitude > 0.01f ? dir : Vector2.right;
            }
            return Random.insideUnitCircle.normalized;
        }

        return ((Vector2)(nearest.position - _casterGO.transform.position)).normalized;
    }

    private void Update()
    {
        if (!_isActive) return;

        // 拖影生成
        _trailTimer -= Time.deltaTime;
        if (_trailTimer <= 0f && _casterSprite != null && _trailMaterial != null)
        {
            _trailTimer = 0.03f;
            SpawnTrail();
        }

        // 卷起的敌人跟随施法者 + 撞墙检测
        if (_carryEnabled)
            UpdateCarried();

        if (_traveled >= _distance)
        {
            EndSlide();
        }
    }

    private void FixedUpdate()
    {
        if (!_isActive) return;

        // 卷起/爆发检测（与位移同步每物理帧检测，确保起点就检测）
        if (_carryEnabled)
        {
            CheckCarry();
            if (!_isActive) return;   // 爆发命中已 EndSlide
        }

        // 按帧移动（transform 直接位移，避开 Kinematic MovePosition 被场景 collider 阻塞的问题）
        float step = _speed * Time.fixedDeltaTime;
        float remaining = _distance - _traveled;
        step = Mathf.Min(step, remaining);

        // 前方碰撞检测：冲刺方向探测墙/障碍 → 撞墙停止（停留在原地）
        Vector2 dir = _direction;
        Collider2D[] frontHits = Physics2D.OverlapCircleAll(
            (Vector2)_casterGO.transform.position + dir * 0.3f, 0.15f, ~0);
        foreach (var fh in frontHits)
        {
            if (fh == null) continue;
            // 跳过自身 collider（冲刺期间已禁用）和敌人（命中由 CheckCarry 处理）
            if (fh.CompareTag("Obstacles") || fh.CompareTag("Wall"))
            {
                EndSlide();
                return;
            }
        }

        if (_casterRb != null)
        {
            _casterRb.velocity = Vector2.zero;
            _casterRb.transform.position += (Vector3)(dir * step);
        }
        else
        {
            _casterGO.transform.position += (Vector3)(dir * step);
        }

        _traveled += step;
    }

    // ──────────────────────────────────────────────
    //  卷起敌人
    // ──────────────────────────────────────────────

    /// <summary>检测途经敌人并卷起</summary>
    private void CheckCarry()
    {
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";

        // 注意：无 mask 重载的 OverlapCircleAll 可能不检测所有层，必须用 ~0 全层
        var hits = Physics2D.OverlapCircleAll(_casterGO.transform.position, _carryRadius, ~0);
        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;

            // 已卷起的跳过
            bool already = false;
            foreach (var c in _carried)
                if (c.col == hit) { already = true; break; }
            if (already) continue;

            // 敌人：排除 Boss（Boss 不可被卷起）、已同化、已死
            var enemy = hit.GetComponent<EnemyCore>();
            if (enemy == null || enemy.IsDead || enemy.IsAssimilated) continue;
            if (enemy.GetComponent<BossCore>() != null) continue;

            // ── 爆发冲击模式：冲撞目标造成伤害 + 周身释放气浪推开周边，不卷走 ──
            if (_burstOnHit)
            {
                if (_burstTriggered) continue;   // 一次冲刺只爆发一次
                _burstTriggered = true;

                // 冲撞伤害
                enemy.Health?.TakeDamage(_carryDamage);

                // 周身释放气浪推开周边敌方单位
                TriggerBurst();

                // 命中即停止冲刺，玩家停留在碰撞处（不继续往前顶/被弹开）
                EndSlide();

                return;
            }

            // 卷起：造成命中伤害 + 吸附
            enemy.Health?.TakeDamage(_carryDamage);

            var carried = new CarriedEnemy
            {
                enemy = enemy,
                col = hit,
                rb = enemy.GetComponent<Rigidbody2D>(),
                wallHit = false
            };

            // 跟随期间无敌（防止被其他伤害打死）
            if (_carriedInvincible)
            {
                var immunity = enemy.GetComponent<DamageImmunity>();
                if (immunity == null)
                    immunity = enemy.gameObject.AddComponent<DamageImmunity>();
                immunity.GrantImmunity(999f);   // 冲刺期间持续无敌
            }

            // 禁用 AI 移动（被卷着走）
            if (enemy.Movement != null)
                enemy.Movement.enabled = false;
            if (enemy.StateMachine != null)
                enemy.StateMachine.enabled = false;

            // 禁用 collider + 物理模拟（防止与施法者物理碰撞阻挡冲刺/推挤）
            if (hit != null)
                hit.enabled = false;
            if (carried.rb != null)
                carried.rb.simulated = false;

            _carried.Add(carried);
        }
    }

    /// <summary>更新卷起敌人的位置（跟随施法者）+ 撞墙检测</summary>
    private void UpdateCarried()
    {
        Vector3 casterPos = _casterGO.transform.position;

        for (int i = _carried.Count - 1; i >= 0; i--)
        {
            var c = _carried[i];
            if (c.enemy == null || c.enemy.IsDead)
            {
                _carried.RemoveAt(i);
                continue;
            }

            // 跟随施法者（禁用 rb 后直接设 transform 位置）
            c.enemy.transform.position = casterPos;
        }
    }

    /// <summary>玩家撞墙：对所有卷起的敌人造成二次伤害并释放</summary>
    private void HitWall()
    {
        if (_carried.Count == 0) return;

        for (int i = _carried.Count - 1; i >= 0; i--)
        {
            var c = _carried[i];
            if (c.enemy == null) continue;

            // 二次伤害前：移除卷起期间的无敌（否则无敌会拦截撞墙伤害）
            var immunity = c.enemy.GetComponent<DamageImmunity>();
            if (immunity != null)
                Object.Destroy(immunity);

            // 二次伤害 + 释放（推离玩家避免重叠）
            c.enemy.transform.position += (Vector3)(-_direction * 1f);
            c.enemy.Health?.TakeDamage(_wallDamage);
            c.wallHit = true;
            ReleaseEnemy(c);
            _carried.RemoveAt(i);
        }
    }

    /// <summary>释放被卷起的敌人（恢复正常 AI + collider + 物理）</summary>
    private void ReleaseEnemy(CarriedEnemy c)
    {
        if (c.enemy == null) return;

        if (c.enemy.Movement != null)
            c.enemy.Movement.enabled = true;
        if (c.enemy.StateMachine != null)
            c.enemy.StateMachine.enabled = true;
        if (c.col != null)
            c.col.enabled = true;
        if (c.rb != null)
            c.rb.simulated = true;
    }

    // ──────────────────────────────────────────────
    //  爆发冲击（包膜爆发冲击等）
    // ──────────────────────────────────────────────

    /// <summary>命中后周身释放气浪：视觉扩散环 + 推开周边敌方单位</summary>
    private void TriggerBurst()
    {
        Vector2 center = _casterGO.transform.position;

        // 1. 气浪视觉（复用扩散波/气浪 shader 材质）
        if (_burstWaveMaterial != null)
            SpawnBurstWave(center);

        // 2. 推开半径内的敌方单位（径向击退）
        string targetTag = _ownerType == Projectile.OwnerType.Player ? "Enemy" : "Player";
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, _burstRadius, ~0);
        foreach (var hit in hits)
        {
            if (hit == null || !hit.CompareTag(targetTag)) continue;

            // 跳过死亡/已同化
            var enemy = hit.GetComponent<EnemyCore>();
            if (enemy != null && (enemy.IsDead || enemy.IsAssimilated)) continue;

            // 径向击退：用击退组件持续位移 + 期间禁 AI 移动（velocity 单次赋值会被敌人 AI 覆盖）
            Vector2 dir = ((Vector2)hit.transform.position - center);
            if (dir.sqrMagnitude < 0.01f)
                dir = _direction;
            else
                dir.Normalize();

            var enemyComp = enemy;
            var rb = hit.GetComponent<Rigidbody2D>();
            if (enemyComp != null)
            {
                // 已有的击退组件则刷新（不叠加多个）
                var push = hit.GetComponent<BurstPushComponent>();
                if (push == null)
                    push = hit.gameObject.AddComponent<BurstPushComponent>();
                push.StartPush(dir, _burstPushForce, enemyComp);
            }
            else if (rb != null)
            {
                rb.velocity = dir * _burstPushForce;
            }
        }
    }

    /// <summary>生成扩散气浪视觉（0→burstRadius 扩散，淡出销毁）</summary>
    private void SpawnBurstWave(Vector2 center)
    {
        var go = new GameObject("BurstWave");
        go.transform.position = center;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.material = new Material(_burstWaveMaterial);
        sr.sprite = CreateWhiteSprite();
        sr.sortingOrder = 60;

        // 气浪动画：sprite 固定为 burstRadius*2，shader _CurrentRadius 驱动扩散
        float scale = _burstRadius * 2f;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var anim = go.AddComponent<BurstWaveAnimator>();
        anim.Init(sr.material, _burstRadius, 0.4f);
    }

    /// <summary>气浪扩散动画组件</summary>
    private class BurstWaveAnimator : MonoBehaviour
    {
        private Material _mat;
        private float _maxRadius;
        private float _duration;
        private float _timer;
        private SpriteRenderer _sr;

        public void Init(Material mat, float maxRadius, float duration)
        {
            _mat = mat;
            _maxRadius = maxRadius;
            _duration = duration;
            _sr = GetComponent<SpriteRenderer>();

            if (_mat != null)
            {
                _mat.SetFloat("_MaxRadius", maxRadius);
                _mat.SetFloat("_CurrentRadius", 0f);
                _mat.SetFloat("_Fade", 1f);
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);

            if (_mat != null)
            {
                _mat.SetFloat("_CurrentRadius", Mathf.Lerp(0f, _maxRadius, t));
                _mat.SetFloat("_Fade", 1f - t);
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

    /// <summary>生成拖影（残影精灵，快速淡出）</summary>
    private void SpawnTrail()
    {
        var go = new GameObject("SlideTrail");
        go.transform.position = _casterGO.transform.position;
        go.transform.rotation = _casterGO.transform.rotation;
        go.transform.localScale = _casterGO.transform.localScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _casterSprite.sprite;
        if (_trailMaterial != null)
            sr.material = new Material(_trailMaterial);
        sr.sortingOrder = _casterSprite.sortingOrder - 1;

        var fade = go.AddComponent<TrailFade>();
        fade.Init(0.25f);
    }

    private void EndSlide()
    {
        if (!_isActive) return;
        _isActive = false;

        // 恢复施法者刚体类型 + collider（冲刺期间改成了 Kinematic + 禁用 collider）
        if (_casterRb != null)
        {
            _casterRb.bodyType = _originalBodyType;
            _casterRb.velocity = Vector2.zero;
        }
        RestoreColliders();

        // 终点：抛出所有卷起的敌人（沿冲刺方向飞行，撞墙触发二次伤害）
        ThrowAllCarried();

        Destroy(this);
    }

    /// <summary>恢复施法者 collider（冲刺期间禁用）</summary>
    private void RestoreColliders()
    {
        if (_casterColliders == null) return;
        foreach (var c in _casterColliders)
            if (c != null) c.enabled = true;
        _casterColliders = null;
    }

    /// <summary>兜底：组件销毁时确保刚体类型 + collider 恢复（防止卡在 Kinematic/禁用）</summary>
    private void OnDestroy()
    {
        if (_casterRb != null && _casterRb.bodyType == RigidbodyType2D.Kinematic)
        {
            _casterRb.bodyType = _originalBodyType;
            _casterRb.velocity = Vector2.zero;
        }
        RestoreColliders();
    }

    /// <summary>抛出所有卷起的敌人（沿冲刺方向飞行 + 撞墙二次伤害）</summary>
    private void ThrowAllCarried()
    {
        if (_carried.Count == 0) return;

        // 抛出距离：冲刺剩余距离或固定值（默认再飞 4 单位）
        float throwDist = Mathf.Max(_distance - _traveled, 3f);

        foreach (var c in _carried)
        {
            if (c.enemy == null) continue;

            // 移除卷起期间的无敌（CarriedThrow 会重新授予抛出期间无敌）
            var immunity = c.enemy.GetComponent<DamageImmunity>();
            if (immunity != null) Object.Destroy(immunity);

            // 恢复 collider + 物理（抛出飞行用）
            if (c.col != null) c.col.enabled = true;
            if (c.rb != null) c.rb.simulated = true;

            // 抛出：沿冲刺方向飞行，撞墙触发二次伤害
            var throwComp = c.enemy.gameObject.AddComponent<CarriedThrow>();
            throwComp.Init(_direction, 10f, throwDist, _wallDamage);
        }

        _carried.Clear();
    }

    /// <summary>拖影淡出组件</summary>
    private class TrailFade : MonoBehaviour
    {
        private float _timer;
        private float _duration;
        private SpriteRenderer _sr;

        public void Init(float duration)
        {
            _duration = duration;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);

            if (_sr != null)
            {
                var c = _sr.color;
                c.a = Mathf.Lerp(0.6f, 0f, t);
                _sr.color = c;
            }

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}

/// <summary>
/// 气浪击退组件 — 沿径向持续位移敌人（速度衰减），期间禁用敌人 AI 移动，
/// 避免 velocity 单次赋值被敌人 AI 下一帧覆盖。位移结束后恢复 AI。
/// </summary>
public class BurstPushComponent : MonoBehaviour
{
    private Vector2 _dir;
    private float _speed;
    private float _elapsed;
    private const float Duration = 0.25f;      // 击退持续时间
    private const float SpeedDecay = 0.6f;     // 每帧速度衰减
    private EnemyCore _enemy;
    private bool _aiDisabled;
    private bool _running;

    /// <summary>开始击退</summary>
    public void StartPush(Vector2 dir, float speed, EnemyCore enemy)
    {
        _dir = dir;
        _speed = speed;
        _enemy = enemy;
        _elapsed = 0f;
        _running = true;

        // 禁用敌人 AI 移动（防止覆盖击退位移）
        if (_enemy != null)
        {
            if (_enemy.Movement != null)
                _enemy.Movement.enabled = false;
            if (_enemy.StateMachine != null)
                _enemy.StateMachine.enabled = false;
            _aiDisabled = true;
        }
    }

    private void FixedUpdate()
    {
        if (!_running) return;

        _elapsed += Time.fixedDeltaTime;

        // 沿径向位移（transform 直接移动，避免物理干扰）
        float step = _speed * Time.fixedDeltaTime;
        transform.position += (Vector3)(_dir * step);

        // 速度衰减
        _speed *= (1f - SpeedDecay * Time.fixedDeltaTime * 4f);

        // 结束
        if (_elapsed >= Duration || _speed < 0.5f)
            Stop();
    }

    private void Stop()
    {
        _running = false;

        // 恢复敌人 AI
        if (_enemy != null && _aiDisabled)
        {
            if (_enemy.Movement != null)
                _enemy.Movement.enabled = true;
            if (_enemy.StateMachine != null)
                _enemy.StateMachine.enabled = true;
        }

        Destroy(this);
    }

    private void OnDestroy()
    {
        if (_running && _enemy != null && _aiDisabled)
        {
            if (_enemy.Movement != null)
                _enemy.Movement.enabled = true;
            if (_enemy.StateMachine != null)
                _enemy.StateMachine.enabled = true;
        }
    }
}
