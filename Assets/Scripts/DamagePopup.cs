using UnityEngine;
using TMPro;

/// <summary>
/// 伤害浮动数字 — 世界空间上浮渐隐。
/// 通过 DamagePopup.Spawn(worldPos, damage) 静态方法创建。
/// </summary>
public class DamagePopup : MonoBehaviour
{
    [Header("动画参数")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _fadeDuration = 0.6f;
    [SerializeField] private float _lifetime = 0.8f;

    private TextMeshPro _text;
    private float _elapsed;

    /// <summary>在指定世界坐标生成伤害数字</summary>
    public static void Spawn(Vector3 worldPosition, float damage, bool isPlayerDamage = false)
    {
        string text = Mathf.Abs(Mathf.RoundToInt(damage)).ToString();
        Color color = isPlayerDamage ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(1f, 0.85f, 0.25f, 1f);
        SpawnInternal(worldPosition, text, color);
    }

    /// <summary>在指定世界坐标生成治疗数字（绿色，带 + 号）</summary>
    public static void SpawnHeal(Vector3 worldPosition, float amount)
    {
        string text = "+" + Mathf.RoundToInt(amount);
        SpawnInternal(worldPosition, text, new Color(0.3f, 0.9f, 0.3f, 1f));
    }

    /// <summary>公共生成逻辑：创建 GO + TMP 并应用上浮渐隐动画</summary>
    private static void SpawnInternal(Vector3 worldPosition, string text, Color color)
    {
        var go = new GameObject("DamagePopup", typeof(DamagePopup));
        go.transform.position = worldPosition + (Vector3)(Random.insideUnitCircle * 0.3f);

        var tmp = go.AddComponent<TextMeshPro>();
        var popup = go.GetComponent<DamagePopup>();
        popup._text = tmp;

        tmp.text = text;
        tmp.fontSize = 8f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.sortingOrder = 100;
        tmp.raycastTarget = false;

        // 描边让文字在任何背景上都看得清
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);

        Object.Destroy(go, popup._lifetime);
    }

    private void Update()
    {
        if (_text == null) return;

        _elapsed += Time.deltaTime;

        // 上浮
        transform.position += Vector3.up * (_moveSpeed * Time.deltaTime);

        // 渐隐
        float alpha = 1f - Mathf.Clamp01(_elapsed / _fadeDuration);
        var c = _text.color;
        _text.color = new Color(c.r, c.g, c.b, alpha);
    }
}
