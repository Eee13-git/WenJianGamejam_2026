using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("射击配置")]
    [SerializeField] private GameObject bulletPrefab;

    private PlayerStats _stats;
    private float _lastShootTime = -10f;

    /// <summary>射击事件委托：参数为 (方向, 子弹速度, 攻击力)</summary>
    public event System.Action<Vector2, float, float> OnShoot;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();

        if (bulletPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("PlayerCombat: 未指定子弹预制体！");
#endif
        }
    }

    /// <summary>尝试射击，由 PlayerController 通过委托调用</summary>
    public void TryShoot()
    {
        if (bulletPrefab == null || _stats == null)
            return;

        if (Time.time < _lastShootTime + _stats.ShootCooldown)
            return;

        // 获取鼠标世界位置
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        Vector2 direction = (mouseWorldPos - transform.position).normalized;

        float bulletSpeed = _stats.BulletSpeed;
        float attackStrength = _stats.AttackStrength;

        // 生成子弹
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Projectile proj = bullet.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(direction, bulletSpeed, attackStrength, Projectile.OwnerType.Player, gameObject);
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogWarning("子弹预制体缺少 Projectile 组件！");
        }
#endif

        _lastShootTime = Time.time;

        OnShoot?.Invoke(direction, bulletSpeed, attackStrength);
    }
}
