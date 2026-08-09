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

    // ── 运行时状态 ──
    private Vector2 _direction;
    private Vector3 _startPos;
    private float _traveled;
    private bool _isActive;
    private float _trailTimer;

    // ── 施法者引用 ──
    private GameObject _casterGO;
    private Rigidbody2D _casterRb;
    private PlayerController _playerController;
    private SpriteRenderer _casterSprite;

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

        // 方向：玩家朝移动方向（无输入则朝鼠标），敌人随机
        if (_playerController != null)
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

        // 冲刺期间无敌
        if (_immuneDuration > 0f)
        {
            var immunity = _casterGO.GetComponent<DamageImmunity>();
            if (immunity == null)
                immunity = _casterGO.AddComponent<DamageImmunity>();
            immunity.GrantImmunity(_immuneDuration);
        }
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

        // 卷起检测（与位移同步每物理帧检测，确保起点就检测）
        if (_carryEnabled)
            CheckCarry();

        // 按帧移动
        float step = _speed * Time.fixedDeltaTime;
        float remaining = _distance - _traveled;
        step = Mathf.Min(step, remaining);

        if (_casterRb != null)
        {
            _casterRb.velocity = Vector2.zero;
            _casterRb.MovePosition(_casterRb.position + _direction * step);
        }
        else
        {
            _casterGO.transform.position += (Vector3)(_direction * step);
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

        // 终点：抛出所有卷起的敌人（沿冲刺方向飞行，撞墙触发二次伤害）
        ThrowAllCarried();

        if (_casterRb != null)
            _casterRb.velocity = Vector2.zero;

        Destroy(this);
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
