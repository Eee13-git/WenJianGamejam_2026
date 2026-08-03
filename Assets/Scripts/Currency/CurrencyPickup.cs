using UnityEngine;

/// <summary>
/// 货币拾取物 — 自动吸附 + 碰撞拾取。
/// 玩家走进 trigger 范围时自动飞向玩家并被收集。
/// </summary>
public class CurrencyPickup : MonoBehaviour
{
    [Header("货币配置")]
    public int amount = 1;

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
        // 超时自毁
        if (Time.time - _spawnTime > _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_player == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);

        if (dist <= _magnetRange)
        {
            // 飞向玩家
            transform.position = Vector3.MoveTowards(
                transform.position,
                _player.position,
                _flySpeed * Time.deltaTime);
        }
        else
        {
            // 漂浮动画
            float t = Time.time * _floatFrequency;
            transform.position = _startPos + Vector3.up * (Mathf.Sin(t) * _floatAmplitude);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;
        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    /// <summary>收集货币</summary>
    public int Collect(GameObject collector)
    {
        if (_collected) return 0;
        _collected = true;

        var manager = collector.GetComponent<CurrencyManager>();
        if (manager != null)
            manager.Add(amount);

        Destroy(gameObject);
        return amount;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _magnetRange);
    }
#endif
}
