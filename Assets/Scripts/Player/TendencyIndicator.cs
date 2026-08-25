using UnityEngine;

/// <summary>
/// 玩家进化倾向头顶指示器：根据进化倾向给角色头顶显示彩色倒三角——
///   t &gt; 0  → 金色（朝向宿主）
///   t &lt; 0  → 紫色（朝向独特）
///   其余    → 蓝色（中性/默认）
/// 由 PlayerManager 在玩家就绪时自动挂载。贴图/材质复用 FootIndicator 的共享倒三角资源，
/// 颜色通过 SpriteRenderer.color 染色，带轻微上下浮动 + 缩放脉冲动画。
/// </summary>
public class TendencyIndicator : MonoBehaviour
{
    [Header("外观")]
    [Tooltip("中性/默认指示器颜色（蓝）")]
    [SerializeField] private Color _neutralColor = new Color(0.30f, 0.62f, 1f, 1f);
    [Tooltip("宿主倾向指示器颜色（金，进化倾向 &gt; 0）")]
    [SerializeField] private Color _hostColor = new Color(1f, 0.84f, 0.25f, 1f);
    [Tooltip("独特倾向指示器颜色（紫，进化倾向 &lt; 0）")]
    [SerializeField] private Color _uniqueColor = new Color(0.70f, 0.38f, 1f, 1f);
    [Tooltip("指示器宽度相对角色 sprite 高度的倍率（0.5 = 宽度为角色高度一半）")]
    [SerializeField] private float _sizeRatio = 0.5f;
    [Tooltip("指示器底部相对角色头顶的悬浮间隙倍率（0.35 = 高出头顶 35% 角色高度）")]
    [SerializeField] private float _hoverOffsetRatio = 0.35f;
    [Tooltip("渲染排序（角色 sprite=2，指示器需在其上方显示）")]
    [SerializeField] private int _sortingOrder = 3;

    [Header("浮动动画")]
    [Tooltip("上下浮动速度（弧度/秒）")]
    [SerializeField] private float _bobSpeed = 2.2f;
    [Tooltip("上下浮动幅度（相对角色高度的比例，0.07 = 7% 角色高度）")]
    [SerializeField] private float _bobAmountRatio = 0.07f;

    [Header("缩放动画")]
    [Tooltip("缩放脉冲速度（弧度/秒）")]
    [SerializeField] private float _pulseSpeed = 2.6f;
    [Tooltip("缩放脉冲幅度（±比例，0.12 = 在 88%~112% 间脉动）")]
    [SerializeField] private float _pulseAmount = 0.12f;

    private PlayerStats _stats;
    private SpriteRenderer _marker;
    private SpriteRenderer _bodySprite;
    private bool _created;
    private float _phase;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _bodySprite = GetComponent<SpriteRenderer>();
        _phase = (Mathf.Abs(GetInstanceID()) * 0.618f) % (Mathf.PI * 2f);
    }

    private void LateUpdate()
    {
        if (_stats == null || _stats.IsDead || !gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        EnsureCreated();
        Show();

        // 颜色：进化倾向 >0 金色 / <0 紫色 / 其余蓝色
        float t = _stats.EvolutionTendency;
        _marker.color = t > 0.001f ? _hostColor : (t < -0.001f ? _uniqueColor : _neutralColor);

        UpdateMarkerTransform();
    }

    private void Hide()
    {
        if (_marker != null)
            _marker.enabled = false;
    }

    private void Show()
    {
        if (_marker != null)
            _marker.enabled = true;
    }

    private void EnsureCreated()
    {
        if (_created) return;
        _created = true;

        var go = new GameObject("TendencyMarker");
        go.transform.SetParent(transform, false);

        _marker = go.AddComponent<SpriteRenderer>();
        _marker.sprite = FootIndicator.GetTriangleSprite();
        _marker.sharedMaterial = FootIndicator.GetTriangleMaterial();
        _marker.sortingOrder = _sortingOrder;
    }

    /// <summary>指示器跟随角色：倒三角悬浮在头顶上方，叠加浮动与缩放动画（与 FootIndicator 同风格）</summary>
    private void UpdateMarkerTransform()
    {
        float worldHeight;
        if (_bodySprite != null)
        {
            worldHeight = _bodySprite.bounds.size.y;
        }
        else
        {
            worldHeight = 1f;
        }

        float maxLossy = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float localHeight = worldHeight / maxLossy;

        float t = Time.time;

        // 缩放脉冲
        float pulse = 1f + Mathf.Sin(t * _pulseSpeed + _phase * 1.7f) * _pulseAmount;

        // 宽度 = 角色高度 × 倍率；贴图 128px、PPU=100 → scale=1 时世界尺寸 1.28
        float targetWidth = worldHeight * _sizeRatio;
        float localScale = (targetWidth / 1.28f) * pulse;
        _marker.transform.localScale = new Vector3(localScale, localScale, 1f);

        // 上下浮动
        float bob = Mathf.Sin(t * _bobSpeed + _phase) * localHeight * _bobAmountRatio;

        // 悬浮在头顶：中心 = 角色顶部 + 悬浮间隙 + 半个三角高度
        float hoverGap = localHeight * _hoverOffsetRatio;
        float triHalfHeight = localScale * 0.7f;
        float localOffset = localHeight * 0.5f + hoverGap + triHalfHeight * 0.3f;
        _marker.transform.localPosition = new Vector3(0f, localOffset + bob, 0f);
    }
}
