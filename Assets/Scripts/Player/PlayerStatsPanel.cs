using UnityEngine;
using UnityEngine.UI;
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
    [Header("属性行文本")]
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private TMP_Text _speedText;
    [SerializeField] private TMP_Text _attackText;
    [SerializeField] private TMP_Text _fireRateText;

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

    private PlayerStats _stats;
    private float _fillTarget;

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
            _evolveBar.color = GetEvolveColor(_stats.EvolutionTendency);
        }
        RefreshEvolveText();
    }

    private void Update()
    {
        if (_stats == null) return;

        RefreshHealth();
        RefreshEvolve();
    }

    /// <summary>生命变化频繁，其余属性低频（Buff 修改时刷新）</summary>
    private void LateUpdate()
    {
        if (_stats == null) return;

        if (_speedText != null)
            _speedText.text = _stats.MoveSpeed.ToString("F1");
        if (_attackText != null)
            _attackText.text = _stats.AttackStrength.ToString("F1");
        if (_fireRateText != null)
            _fireRateText.text = _stats.ShotsPerMinute.ToString("F0") + "/分";
    }

    private void RefreshHealth()
    {
        if (_healthText != null)
            _healthText.text = $"{_stats.CurrentHealth:F0} / {_stats.MaxHealth:F0}";
    }

    private void RefreshEvolve()
    {
        RefreshFillTarget();

        // 平滑过渡到目标填充量（自动填充动画）
        if (_evolveBar != null)
        {
            _evolveBar.fillAmount = Mathf.MoveTowards(
                _evolveBar.fillAmount, _fillTarget, _evolveAnimSpeed * Time.deltaTime);

            // 颜色按正负切换
            _evolveBar.color = GetEvolveColor(_stats.EvolutionTendency);
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
            _evolveValueText.text = $"宿主 +{val:F0}";
        else if (val < -0.01f)
            _evolveValueText.text = $"独特 {val:F0}";
        else
            _evolveValueText.text = "平衡 0";
    }
}
