using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 玩家属性面板 — 显示角色各项属性（生命/移速/攻击/射速）与进化倾向。
/// 进化倾向用单个槽显示：填充量 = |值| / max（0 无填充，|100| 填满），
/// 颜色按正负切换：正值=朝向宿主（金色），负值=朝向独特（紫色）。
/// 槽位根据数值自动填充，数值变化时平滑过渡（动画填充）。
/// 挂在 UICanvas/StatsPanel 上，每帧从 PlayerStats 刷新数据。
/// </summary>
public class PlayerStatsPanel : MonoBehaviour
{
    [Header("血条")]
    [Tooltip("血条填充 Image（Filled/Horizontal/Left），使用 血条.png")]
    [SerializeField] private Image _healthBarFill;

    [Header("属性行文本")]
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private TMP_Text _speedText;
    [SerializeField] private TMP_Text _attackText;
    [SerializeField] private TMP_Text _fireRateText;
    [SerializeField] private TMP_Text _bulletSpeedText;

    [Header("进化倾向")]
    [Tooltip("进化倾向槽（单槽），fillMethod=Horizontal, fillOrigin=Left，按 |值|/max 填充")]
    [SerializeField] private Image _evolveBar;
    [SerializeField] private TMP_Text _evolveValueText;

    [Header("配置")]
    [Tooltip("进化倾向最大值（用于槽位归一化）")]
    [SerializeField] private float _evolveMax = 100f;
    [Tooltip("槽位填充动画速度（每秒填充比例，2 = 0.5秒填满）")]
    [SerializeField] private float _evolveAnimSpeed = 3f;

    [Header("进化倾向颜色")]
    [Tooltip("正值（朝向宿主）颜色：金色")]
    [SerializeField] private Color _hostColor = new Color(1f, 0.8f, 0.25f, 1f);
    [Tooltip("负值（朝向独特）颜色：紫色")]
    [SerializeField] private Color _uniqueColor = new Color(0.65f, 0.35f, 1f, 1f);
    [Tooltip("零值颜色（无填充时基本不可见）")]
    [SerializeField] private Color _zeroColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("进化倾向 Tooltip（悬停公式详情）")]
    [Tooltip("进化倾向公式详情面板预制体（独立 prefab；为空时回退为代码动态创建）")]
    [SerializeField] private GameObject _evolveTooltipPrefab;
    [Tooltip("悬停进化倾向槽时显示公式形式加成详情")]
    [SerializeField] private bool _enableEvolveTooltip = true;
    [Tooltip("Tooltip 宽度（像素）")]
    [SerializeField] private float _tooltipWidth = 340f;
    [Tooltip("Tooltip 高度（像素）")]
    [SerializeField] private float _tooltipHeight = 250f;
    [Tooltip("Tooltip 相对面板右上角的偏移")]
    [SerializeField] private Vector2 _tooltipOffset = new Vector2(6f, -90f);

    private PlayerStats _stats;
    private float _fillTarget;

    // Tooltip 运行时对象
    private GameObject _tooltipRoot;
    private TMP_Text _tooltipText;
    private Coroutine _hideCoroutine;

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _stats = player.GetComponent<PlayerStats>();

        // PlayerManager 持久化，Tag=Player 的物体一定在场景中
        if (_stats == null)
        {
            var mgr = FindObjectOfType<PlayerManager>(true);
            if (mgr != null && mgr.CurrentPlayer != null)
                _stats = mgr.CurrentPlayer.GetComponent<PlayerStats>();
        }

        if (_stats == null)
        {
            Debug.LogWarning("PlayerStatsPanel: 找不到 PlayerStats，面板禁用");
            enabled = false;
            return;
        }

        // 初始同步：直接设置目标值（无动画，避免从 0 慢速涨起）
        RefreshFillTarget();
        if (_evolveBar != null)
        {
            _evolveBar.fillAmount = _fillTarget;
            // 使用贴图本色，不着色
        }
        RefreshEvolveText();

        // 悬停公式 Tooltip（动态创建，不改 prefab 序列化）
        if (_enableEvolveTooltip && _evolveBar != null)
            CreateEvolveTooltip();
    }

    private void Update()
    {
        if (_stats == null) return;

        RefreshHealth();
        RefreshEvolve();

        // Tooltip 显示期间实时刷新数值（倾向/射速变化即时反映）
        if (_tooltipRoot != null && _tooltipRoot.activeSelf)
            _tooltipText.text = BuildEvolveFormulaText();
    }

    /// <summary>生命变化频繁，其余属性低频（Buff 修改时刷新）</summary>
    private void LateUpdate()
    {
        if (_stats == null) return;

        if (_speedText != null)
            _speedText.text = _stats.MoveSpeed.ToString("F1");
        if (_attackText != null)
        {
            float mult = _stats.AttackStrengthMultiplier;
            if (mult != 1f)
                _attackText.text = $"{_stats.AttackStrength:F1}（{_stats.BaseAttackStrength:F0}×{mult:F1}）";
            else
                _attackText.text = _stats.AttackStrength.ToString("F1");
        }
        if (_fireRateText != null)
            _fireRateText.text = _stats.ShotsPerMinute.ToString("F0") + "/分";
        if (_bulletSpeedText != null)
            _bulletSpeedText.text = _stats.BulletSpeed.ToString("F1");
    }

    private void RefreshHealth()
    {
        if (_healthText != null)
            _healthText.text = $"{_stats.CurrentHealth:F0} / {_stats.MaxHealth:F0}";
        if (_healthBarFill != null)
            _healthBarFill.fillAmount = Mathf.Clamp01(_stats.CurrentHealth / Mathf.Max(_stats.MaxHealth, 1f));
    }

    private void RefreshEvolve()
    {
        RefreshFillTarget();

        // 平滑过渡到目标填充量（自动填充动画）
        if (_evolveBar != null)
        {
            _evolveBar.fillAmount = Mathf.MoveTowards(
                _evolveBar.fillAmount, _fillTarget, _evolveAnimSpeed * Time.deltaTime);
            // 使用贴图本色，不着色
        }

        RefreshEvolveText();
    }

    /// <summary>根据当前进化倾向计算填充目标（|值|/max）</summary>
    private void RefreshFillTarget()
    {
        float val = _stats.EvolutionTendency;
        _fillTarget = Mathf.Clamp01(Mathf.Abs(val) / Mathf.Max(_evolveMax, 0.01f));
    }

    /// <summary>按正负返回颜色：正值=金色（宿主），负值=紫色（独特），0=中性</summary>
    private Color GetEvolveColor(float val)
    {
        if (val > 0.01f) return _hostColor;
        if (val < -0.01f) return _uniqueColor;
        return _zeroColor;
    }

    private void RefreshEvolveText()
    {
        if (_evolveValueText == null) return;

        float val = _stats.EvolutionTendency;
        if (val > 0.01f)
        {
            _evolveValueText.text = $"宿主 +{val:F0}";
            _evolveValueText.color = _hostColor;
        }
        else if (val < -0.01f)
        {
            _evolveValueText.text = $"独特 {val:F0}";
            _evolveValueText.color = _uniqueColor;
        }
        else
        {
            _evolveValueText.text = "平衡 0";
            _evolveValueText.color = _zeroColor;
        }
    }

    // ════════════════════════════════════════════
    //  进化倾向悬停 Tooltip（公式形式）
    // ════════════════════════════════════════════

    /// <summary>创建 Tooltip（优先实例化独立 prefab，回退代码动态创建）+ 槽位悬停事件</summary>
    private void CreateEvolveTooltip()
    {
        if (_evolveTooltipPrefab != null)
        {
            // ── 方式 A：实例化独立预制体 ──
            _tooltipRoot = Instantiate(_evolveTooltipPrefab, transform);
            _tooltipRoot.name = "EvolveTooltip";

            // 锚定面板右上角外侧（避免遮挡属性文本），pivot 左上
            var rt = _tooltipRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = _tooltipOffset;
            rt.sizeDelta = new Vector2(_tooltipWidth, _tooltipHeight);

            // 文本组件在 prefab 中为子对象 TipText
            var tip = _tooltipRoot.transform.Find("TipText");
            if (tip != null)
                _tooltipText = tip.GetComponent<TMP_Text>();
            if (_tooltipText != null)
            {
                _tooltipText.raycastTarget = false;
                _tooltipText.text = "";
            }
        }
        else
        {
            // ── 方式 B：代码动态创建（向后兼容）──
            _tooltipRoot = new GameObject("EvolveTooltip", typeof(RectTransform));
            _tooltipRoot.transform.SetParent(transform, false);
            var rt = _tooltipRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = _tooltipOffset;
            rt.sizeDelta = new Vector2(_tooltipWidth, _tooltipHeight);

            var bg = _tooltipRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.88f);
            bg.raycastTarget = false;

            var txtGO = new GameObject("TipText", typeof(RectTransform));
            txtGO.transform.SetParent(_tooltipRoot.transform, false);
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 8f);
            trt.offsetMax = new Vector2(-10f, -8f);

            _tooltipText = txtGO.AddComponent<TextMeshProUGUI>();
            _tooltipText.fontSize = 14f;
            _tooltipText.color = Color.white;
            _tooltipText.alignment = TextAlignmentOptions.TopLeft;
            _tooltipText.enableWordWrapping = true;
            _tooltipText.raycastTarget = false;
            _tooltipText.text = "";
        }

        _tooltipRoot.SetActive(false);

        // ── 悬停热区 ──
        // 关键修复：槽位内 EvolveValue 文本是 stretch 全槽（TMP 默认 raycastTarget=true），
        // 会拦截整个槽的射线；而事件若只挂在 _evolveBar（填充条）上，悬停永远不触发。
        // 因此事件同时挂在：
        //   1) _evolveValueText（覆盖整个槽的文本，悬停槽任意位置都命中）
        //   2) _evolveBar（兜底，并强制其 raycastTarget=true）
        AttachHover(_evolveValueText != null ? _evolveValueText.gameObject : _evolveBar.gameObject);
        AttachHover(_evolveBar.gameObject);
        // 两个热区都强制可被射线命中（prefab 里文本 raycastTarget 可能被关闭）
        _evolveBar.raycastTarget = true;
        if (_evolveValueText != null)
            _evolveValueText.raycastTarget = true;
    }

    /// <summary>在指定对象上挂悬停显示/隐藏事件</summary>
    private void AttachHover(GameObject target)
    {
        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowEvolveTooltip());
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ScheduleHide());
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private void ShowEvolveTooltip()
    {
        if (_tooltipRoot == null || _stats == null) return;

        // 取消延迟隐藏（鼠标回到槽内/进入热区时不隐藏）
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        _tooltipText.text = BuildEvolveFormulaText();
        _tooltipRoot.SetActive(true);
        _tooltipRoot.transform.SetAsLastSibling();
    }

    private void ScheduleHide()
    {
        if (_hideCoroutine != null)
            StopCoroutine(_hideCoroutine);
        // 延迟隐藏：鼠标从槽移向 tooltip 阅读公式时不会中途消失
        _hideCoroutine = StartCoroutine(HideAfterDelay(0.5f));
    }

    private System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _hideCoroutine = null;
        if (_tooltipRoot != null)
            _tooltipRoot.SetActive(false);
    }

    /// <summary>
    /// 构建进化倾向加成详情（公式形式，代入当前数值）。
    /// 与 EnemyFollower/PlayerStats 的实现公式严格一致：
    ///   - 角色技能强化：倍率 = 1 + t × dmgFactor（t>0 宿主方向生效）
    ///   - 随从属性强化：倍率 = max(0, 1 + (-t) × followerFactor)（t<0 独特方向生效，与 EnemyFollower 一致，不用绝对值）
    ///   - 随从冷却映射：冷却乘区 = clamp(1 - max(0, 射速-基准) × 系数, 下限, 1)
    /// </summary>
    private string BuildEvolveFormulaText()
    {
        float t = _stats.EvolutionTendency;
        float dmgFactor = _stats.EvolveSkillDamageFactor;
        float followerFactor = _stats.EvolveFollowerBuffFactor;
        float shots = _stats.ShotsPerMinute;

        string tDesc = t > 0.01f ? $"宿主 +{t:F0}" : (t < -0.01f ? $"独特 {t:F0}" : "平衡 0");
        string hostActive = t > 0.01f ? "（当前生效）" : "（当前未生效）";
        string uniqueActive = t < -0.01f ? "（当前生效）" : "（当前未生效）";
        float hostMult = 1f + t * dmgFactor;
        float uniqueMult = Mathf.Max(0f, 1f + (-t) * followerFactor);
        float cdFactor = EnemyFollower.GetShotsCooldownFactor(_stats);
        float cdReductionPct = (1f - cdFactor) * 100f;

        return $"进化倾向加成　t = {t:F0}（{tDesc}）\n\n" +
               $"【角色技能强化】\n" +
               $"技能伤害倍率 = 1 + t × {dmgFactor}\n" +
               $"           = 1 + {t:F0} × {dmgFactor} = {hostMult:F2}{hostActive}\n\n" +
               $"【随从属性强化】\n" +
               $"随从属性倍率 = max(0, 1 + (-t) × {followerFactor})\n" +
               $"           = max(0, 1 + ({-t:F0}) × {followerFactor}) = {uniqueMult:F2}{uniqueActive}\n\n" +
               $"【随从冷却映射】\n" +
               $"冷却乘区 = 1 - max(0, 射速-{EnemyFollower.ShotsBasePerMinute:F0}) × {EnemyFollower.ShotsToCooldownFactor}\n" +
               $"        = 1 - max(0, {shots:F0}-{EnemyFollower.ShotsBasePerMinute:F0}) × {EnemyFollower.ShotsToCooldownFactor} = {cdFactor:F2}\n" +
               $"（射速 {shots:F0}/分 → 随从冷却缩短 {cdReductionPct:F0}%）";
    }
}
