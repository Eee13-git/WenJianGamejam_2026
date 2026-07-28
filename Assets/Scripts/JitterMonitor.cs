using UnityEngine;
using System.Text;

public class JitterMonitor : MonoBehaviour
{
    public float logInterval = 0.1f;
    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= logInterval)
        {
            timer = 0f;
            var enemies = FindObjectsOfType<Enemy1>();
            var player = GameObject.FindGameObjectWithTag("Player");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"T={Time.time:F2} Player:{player?.transform.position}");
            foreach (var e in enemies)
            {
                var rb = e.GetComponent<Rigidbody2D>();
                sb.AppendLine($"  {e.name}: ({e.transform.position.x:F3},{e.transform.position.y:F3}) vel=({rb.velocity.x:F2},{rb.velocity.y:F2})");
            }
            Debug.Log(sb.ToString());
        }
    }
}
