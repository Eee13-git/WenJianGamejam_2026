using UnityEngine;

/// <summary>
/// 头上阵营指示器：普通敌人头顶悬浮红色倒三角，随从/玩家方单位头顶悬浮绿色倒三角，Boss 不显示。
/// 由 EnemyCore.Awake 自动挂载（所有敌人天然获得，含动态生成的噬菌体/召唤物）。
/// 倒三角贴图为程序化生成（静态共享，首次使用时生成一次），颜色通过 SpriteRenderer.color 染色。
/// 自带轻微上下浮动 + 缩放脉冲动画（每单位独立相位，避免全体同步显得呆板）。
/// </summary>
public class FootIndicator : MonoBehaviour
{
    [Header("外观")]
    [Tooltip("随从/友军指示器颜色")]
    [SerializeField] private Color _followerColor = new Color(0.35f, 1f, 0.45f, 1f);
    [Tooltip("敌人指示器颜色")]
    [SerializeField] private Color _enemyColor = new Color(1f, 0.28f, 0.22f, 1f);
    [Tooltip("指示器宽度相对单位 sprite 高度（或碰撞直径）的倍率（0.5 = 宽度为单位高度一半）")]
    [SerializeField] private float _sizeRatio = 0.5f;
    [Tooltip("指示器底部相对单位头顶的悬浮间隙倍率（0.35 = 高出头顶 35% 单位高度）")]
    [SerializeField] private float _hoverOffsetRatio = 0.35f;
    [Tooltip("渲染排序（单位 sprite=2，指示器需在其上方显示）")]
    [SerializeField] private int _sortingOrder = 3;

    [Header("浮动动画")]
    [Tooltip("上下浮动速度（弧度/秒）")]
    [SerializeField] private float _bobSpeed = 2.2f;
    [Tooltip("上下浮动幅度（相对单位高度的比例，0.07 = 7% 单位高度）")]
    [SerializeField] private float _bobAmountRatio = 0.07f;

    [Header("缩放动画")]
    [Tooltip("缩放脉冲速度（弧度/秒）")]
    [SerializeField] private float _pulseSpeed = 2.6f;
    [Tooltip("缩放脉冲幅度（±比例，0.12 = 在 88%~112% 间脉动）")]
    [SerializeField] private float _pulseAmount = 0.12f;

    private EnemyCore _core;
    private BossCore _boss;
    private SpriteRenderer _marker;
    private CircleCollider2D _collider;
    private SpriteRenderer _bodySprite;
    private bool _created;
    private float _phase; // 每单位独立的动画相位

    // ── 静态共享资源（首次使用生成一次，domain reload 后自动重建） ──
    private static Sprite _triangleSprite;
    private static Material _triangleMaterial;

    private void Awake()
    {
        _core = GetComponent<EnemyCore>();
        _boss = GetComponent<BossCore>();
        _collider = GetComponent<CircleCollider2D>();
        _bodySprite = GetComponent<SpriteRenderer>();

        // 用实例 ID 派生独立相位，让不同单位的浮动/缩放错开
        _phase = (Mathf.Abs(GetInstanceID()) * 0.618f) % (Mathf.PI * 2f);
    }

    private void LateUpdate()
    {
        if (_core == null) return;

        // Boss 不显示指示器
        if (_boss != null) { Hide(); return; }

        // 死亡/未激活隐藏
        if (_core.Health != null && _core.Health.IsDead) { Hide(); return; }
        if (!gameObject.activeInHierarchy) { Hide(); return; }

        EnsureCreated();
        Show();

        // 阵营颜色：随从（同化/tag=Player）→ 绿色；敌人 → 红色
        bool friendly = _core.IsAssimilated || gameObject.CompareTag("Player");
        _marker.color = friendly ? _followerColor : _enemyColor;

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

        var go = new GameObject("HeadMarker");
        go.transform.SetParent(transform, false);

        _marker = go.AddComponent<SpriteRenderer>();
        _marker.sprite = GetTriangleSprite();
        _marker.sharedMaterial = GetTriangleMaterial();
        _marker.sortingOrder = _sortingOrder;
    }

    /// <summary>
    /// 指示器位置/尺寸跟随单位：倒三角悬浮在单位头顶上方，宽度略小于单位宽度；
    /// 叠加正弦浮动（上下）与缩放脉冲（呼吸感），每单位相位独立。
    /// </summary>
    private void UpdateMarkerTransform()
    {
        // 世界高度：优先 sprite 渲染边界，回退碰撞直径
        float worldHeight;
        if (_bodySprite != null)
        {
            worldHeight = _bodySprite.bounds.size.y;
        }
        else if (_collider != null)
        {
            float s = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            worldHeight = _collider.radius * 2f * s;
        }
        else
        {
            worldHeight = 1f;
        }

        float maxLossy = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float localHeight = worldHeight / maxLossy;

        float t = Time.time;

        // 缩放脉冲（在基准 scale 上叠加正弦）
        float pulse = 1f + Mathf.Sin(t * _pulseSpeed + _phase * 1.7f) * _pulseAmount;

        // 宽度 = 单位高度 × 倍率；贴图 128px、PPU=100 → scale=1 时世界尺寸 1.28
        float targetWidth = worldHeight * _sizeRatio;
        float localScale = (targetWidth / 1.28f) * pulse;
        _marker.transform.localScale = new Vector3(localScale, localScale, 1f);

        // 上下浮动（在基准头顶位置叠加正弦偏移）
        float bob = Mathf.Sin(t * _bobSpeed + _phase) * localHeight * _bobAmountRatio;

        // 悬浮在头顶：三角中心 = 单位顶部（局部半高）+ 悬浮间隙 + 半个三角高度（使三角底边贴头顶上方间隙）
        float hoverGap = localHeight * _hoverOffsetRatio;
        float triHalfHeight = localScale * 0.7f; // 三角高 = 0.7 倍 scale
        float localOffset = localHeight * 0.5f + hoverGap + triHalfHeight * 0.3f;
        _marker.transform.localPosition = new Vector3(0f, localOffset + bob, 0f);
    }

    // ── 程序化倒三角贴图（白色，由 color 染色） ──

    /// <summary>生成/获取共享倒三角贴图（供 FootIndicator / TendencyIndicator 复用）</summary>
    public static Sprite GetTriangleSprite()
    {
        if (_triangleSprite == null)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            // 倒三角三个顶点（UV 空间，y 向上，尖端朝下）。
            // 顶点顺序 A→B→C 为顺时针，因此内部点对三条边的叉积均 ≤ 0。
            Vector2 A = new Vector2(-0.68f, 0.66f); // 左上
            Vector2 B = new Vector2( 0.68f, 0.66f); // 右上
            Vector2 C = new Vector2( 0f,   -0.66f); // 底部尖角

            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x - center) / center, (y - center) / center);

                    // 三角形内部判定：顺时针三边叉积全部 ≤ 0
                    if (Cross(B - A, p - A) <= 0f && Cross(C - B, p - B) <= 0f && Cross(A - C, p - C) <= 0f)
                    {
                        // 中心略亮、边缘略透的渐变，增强立体感
                        float dist = Mathf.Min(
                            DistanceToEdge(p, A, B),
                            Mathf.Min(DistanceToEdge(p, B, C), DistanceToEdge(p, C, A)));
                        float a = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(dist * 2.5f));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            _triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
        return _triangleSprite;
    }

    /// <summary>生成/获取共享 Unlit 材质（供 FootIndicator / TendencyIndicator 复用）</summary>
    public static Material GetTriangleMaterial()
    {
        if (_triangleMaterial == null)
        {
            // 指示器是 UI 语义提示：用 Unlit 不受 2D 光照影响，颜色恒定清晰
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader != null)
                _triangleMaterial = new Material(shader);
        }
        return _triangleMaterial;
    }

    /// <summary>2D 叉积（z 分量）</summary>
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    /// <summary>点到线段的最短距离（用于边缘渐变）</summary>
    private static float DistanceToEdge(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
