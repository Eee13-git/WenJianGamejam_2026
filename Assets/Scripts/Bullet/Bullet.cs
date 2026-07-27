using UnityEngine;

public class Projectile : MonoBehaviour
{
    public enum OwnerType
    {
        Player, // 玩家发射：碰到敌人造成伤害
        Enemy   // 敌人发射：碰到玩家造成伤害
    }

    [Header("子弹参数")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 3f; // 自动销毁时间

    private Vector2 direction;
    private OwnerType owner;
    private bool isInitialized = false;

    // ---------- 初始化方法（由发射者调用） ----------
    public void Initialize(Vector2 dir, float spd, float dmg, OwnerType own)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        owner = own;
        isInitialized = true;

        // 自动销毁，防止子弹无限飞行
        Destroy(gameObject, lifeTime);
    }

    // 简便重载：玩家默认调用（可省略 speed/damage 参数）
    public void Initialize(Vector2 dir, OwnerType own)
    {
        Initialize(dir, speed, damage, own);
    }

    void Update()
    {
        if (!isInitialized) return;
        // 每帧移动
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    // ---------- 碰撞检测 ----------
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInitialized) return;

        // 1. 根据所有者决定伤害目标
        if (owner == OwnerType.Player)
        {
            // 玩家子弹 -> 伤害敌人
            BaseEnemy enemy = other.GetComponent<BaseEnemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
        }
        else if (owner == OwnerType.Enemy)
        {
            // 敌人子弹 -> 伤害玩家
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
        }

        // 2. 碰到障碍物（墙壁）销毁
        // 方法一：通过 Tag 检测（需要给 Obstacles Tilemap 的碰撞体加上 "Obstacles" Tag）
        if (other.CompareTag("Obstacles"))
        {
            Destroy(gameObject);
            return;
        }

        // 方法二（更推荐）：通过 MapManager 检测目标位置是否可通行
        // 但这里用 Tag 更简单，提前给墙壁碰撞体设置 Tag 即可
    }

    // ---------- 可选：边缘销毁（防止飞出地图边界） ----------
    private void OnBecameInvisible()
    {
        // 当子弹离开相机视野时销毁（节省性能）
        Destroy(gameObject);
    }
}