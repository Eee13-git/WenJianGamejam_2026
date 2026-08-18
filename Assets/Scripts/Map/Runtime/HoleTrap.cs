using System.Collections;
using UnityEngine;

/// <summary>
/// 洞陷阱：踏入时扣血+逐渐缩小陷入+延迟后弹出恢复。
/// 玩家、敌人、随从均可触发。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HoleTrap : MonoBehaviour
{
    [Header("陷阱参数")]
    [Tooltip("扣血比例（占最大生命）")]
    [SerializeField] private float _damageRatio = 0.1f;
    [Tooltip("陷入洞中的持续时间（秒），期间逐渐缩小")]
    [SerializeField] private float _trapDuration = 2f;
    [Tooltip("缩小目标倍率")]
    [SerializeField] private float _shrinkScale = 0.2f;
    [Tooltip("弹出后离洞中心的偏移距离")]
    [SerializeField] private float _popOffset = 1.5f;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>运行时配置陷阱参数（由编辑器/制造器调用）</summary>
    public void Configure(float damageRatio, float trapDuration, float shrinkScale, float popOffset)
    {
        _damageRatio = damageRatio;
        _trapDuration = trapDuration;
        _shrinkScale = shrinkScale;
        _popOffset = popOffset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        float maxHP;
        System.Action unlock;

        var playerStats = other.GetComponent<PlayerStats>();
        var enemyCore = other.GetComponent<EnemyCore>();

        if (playerStats != null && enemyCore == null)
        {
            // 玩家
            if (playerStats.IsDead) return;
            maxHP = playerStats.MaxHealth;
            var controller = other.GetComponent<PlayerController>();
            if (controller == null) return;
            controller.InputLocked = true;
            controller.AttackLocked = true;
            unlock = () => { controller.InputLocked = false; controller.AttackLocked = false; };
        }
        else if (enemyCore != null)
        {
            // 敌人或随从
            if (enemyCore.IsDead) return;
            maxHP = enemyCore.Health.MaxHealth;
            var sm = enemyCore.StateMachine;
            var follower = other.GetComponent<EnemyFollower>();

            bool smWasEnabled = sm != null && sm.enabled;
            bool followerWasActive = follower != null && follower.IsActive;

            if (sm != null) sm.enabled = false;
            if (follower != null) follower.SetPaused(true);

            unlock = () =>
            {
                if (smWasEnabled && sm != null) sm.enabled = true;
                if (followerWasActive && follower != null) follower.SetPaused(false);
            };
        }
        else
        {
            return;
        }

        var sr = other.GetComponent<SpriteRenderer>();
        var damageable = other.GetComponent<IDamageable>();
        if (damageable == null) return;

        StartCoroutine(TrapRoutine(damageable, maxHP, rb, sr, unlock));
    }

    private IEnumerator TrapRoutine(IDamageable target, float maxHP,
        Rigidbody2D rb, SpriteRenderer sr, System.Action unlock)
    {
        // 1. 扣血
        target.TakeDamage(maxHP * _damageRatio);

        // 2. 停止移动
        rb.velocity = Vector2.zero;

        // 3. 移到洞中心
        Vector2 holeCenter = transform.position;
        Vector2 originalPos = rb.position;
        rb.position = holeCenter;

        // 4. 逐渐缩小（在 trapDuration 期间）
        Vector3 originalScale = sr != null ? sr.transform.localScale : Vector3.one;
        if (sr != null)
        {
            float elapsed = 0f;
            while (elapsed < _trapDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _trapDuration);
                sr.transform.localScale = Vector3.Lerp(originalScale, originalScale * _shrinkScale, t);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(_trapDuration);
        }

        // 5. 弹出到洞旁（远离进入方向）
        Vector2 popDir = (originalPos - holeCenter).normalized;
        if (popDir == Vector2.zero) popDir = Random.insideUnitCircle.normalized;
        rb.position = holeCenter + popDir * _popOffset;

        // 6. 直接恢复原始缩放
        if (sr != null)
            sr.transform.localScale = originalScale;

        // 7. 解锁
        unlock?.Invoke();
    }
}
