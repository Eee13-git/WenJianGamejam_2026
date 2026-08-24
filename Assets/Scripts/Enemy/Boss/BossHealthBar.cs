using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 血条 — 屏幕顶部显示 Boss 血条（含名字）。
/// 单例，Boss 出生时由 BossCore 触发显示，Boss 死亡时隐藏。
/// 持有 EnemyStats 引用，每帧轮询当前/最大生命值更新填充（与 PlayerStatsPanel 同一 MVC 模式）。
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    public static BossHealthBar Instance { get; private set; }

    [Header("引用")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Text _nameText;

    private Font _nameFont;
    private EnemyStats _stats;
    private float _maxHp = 1f;
    private bool _visible;

    private void Awake()
    {
        Instance = this;
        // 全局像素字体（旧版 Text 组件需 Font 资产）
        _nameFont = Resources.Load<Font>("Fonts/ark-pixel-12px-monospaced-zh_cn");
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>显示 Boss 血条并绑定数据源</summary>
    public void Show(string bossName, EnemyStats stats)
    {
        _stats = stats;
        _maxHp = Mathf.Max(stats != null ? stats.MaxHealth : 1f, 1f);
        if (_nameText != null)
        {
            if (_nameFont != null) _nameText.font = _nameFont;
            _nameText.text = bossName;
        }
        SetVisible(true);

        // 立即同步一次
        if (_stats != null && _fillImage != null)
            _fillImage.fillAmount = Mathf.Clamp01(_stats.CurrentHealth / _maxHp);
    }

    private void Update()
    {
        if (!_visible || _stats == null) return;

        // 每帧轮询（与 PlayerStatsPanel 同一模式）
        float ratio = Mathf.Clamp01(_stats.CurrentHealth / Mathf.Max(_stats.MaxHealth, 1f));
        if (_fillImage != null)
            _fillImage.fillAmount = ratio;
    }

    /// <summary>隐藏 Boss 血条</summary>
    public void Hide()
    {
        _stats = null;
        SetVisible(false);
    }

    private void SetVisible(bool v)
    {
        _visible = v;
        if (_panel != null) _panel.SetActive(v);
    }
}
