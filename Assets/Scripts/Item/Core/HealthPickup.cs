using UnityEngine;

/// <summary>
/// 葡萄糖（血量回复物）— 自动吸附 + 碰撞拾取，逻辑复刻 CurrencyPickup。
/// 玩家走进 trigger 范围时自动飞向玩家并回血。
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [Header("回血配置")]
    public float healAmount = 20f;

    [Header("自动吸附")]
    [SerializeField] private float _magnetRange = 2.5f;
    [SerializeField] private float _flySpeed = 8f;
    [SerializeField] private float _lifetime = 15f;

    [Header("漂浮动画")]
    [SerializeField] private float _floatAmplitude = 0.1f;
    [SerializeField] private float _floatFrequency = 3f;

    private Transform _player;
    private float _spawnTime;
    private Vector3 _startPos;
    private bool _collected;

    private void Start()
    {
        _spawnTime = Time.time;
        _startPos = transform.position;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
            _player = playerGo.transform;
    }

    private void Update()
    {
        if (Time.time - _spawnTime > _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_player == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);

        if (dist <= _magnetRange)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                _player.position,
                _flySpeed * Time.deltaTime);
        }
        else
        {
            float t = Time.time * _floatFrequency;
            transform.position = _startPos + Vector3.up * (Mathf.Sin(t) * _floatAmplitude);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;
        if (other.CompareTag("Player"))
            Collect(other.gameObject);
    }

    /// <summary>拾取回血</summary>
    public void Collect(GameObject collector)
    {
        if (_collected) return;
        _collected = true;

        var stats = collector.GetComponent<PlayerStats>();
        if (stats != null)
            stats.Heal(healAmount);

        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _magnetRange);
    }
#endif
}
